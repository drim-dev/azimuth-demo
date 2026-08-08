using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pricing;
using Trips.Database;
using Trips.Domain;
using Trips.Features.Dispatch;

namespace Trips.Features.Trips;

/// <summary>
/// The sole constructor of a trip: resolves the quote and rejects on absence or expiry before any
/// write, then fans the trip out to available drivers.
/// </summary>
public static class RequestRide
{
    private const string PickupArea = "downtown";

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/trips", async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(request, ct)));
    }

    public sealed record Request(string RiderId, string QuoteToken) : IRequest<Response>;

    public sealed record Response(
        string TripId,
        string State,
        long FareMinor,
        string Currency,
        bool AwaitingDriver,
        int DriversOffered);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.RiderId).NotEmpty();
            RuleFor(x => x.QuoteToken).NotEmpty();
        }
    }

    public sealed class RequestHandler(
        TripDbContext db,
        ISender sender,
        IdFactory ids,
        Clock clock,
        QuoteTokenCodec tokens)
        : IRequestHandler<Request, Response>
    {
        /// <summary>
        /// Admits a request and creates the trip, in one transaction.
        /// </summary>
        /// <remarks>
        /// Quote validation and both uniqueness rules are settled here against real storage: the
        /// checks read well and produce the error the rider sees, but the two partial unique indexes
        /// are what hold when two requests arrive together.
        /// </remarks>
        [Realizes("trips/request", "request-admitted-with-valid-quote")]
        [Realizes("trips/request", "request-rejected-with-expired-quote")]
        [Realizes("trips/request", "request-rejected-with-unknown-quote")]
        [Realizes("trips/request", "quote-consumed-once")]
        [Realizes("trips/request", "trip-created-in-requested-state")]
        [Realizes("trips/request", "rider-informed-of-trip")]
        [Realizes("trips/request", "second-request-rejected-while-active")]
        [Realizes("trips/request", "request-admitted-after-terminal")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var trip = await AdmitAsync(request, ct);
            var offered = await sender.Send(new OfferTripToDrivers.Request(trip.Id, PickupArea), ct);

            return new Response(
                IdEncoding.Encode(trip.Id),
                TripStateMachine.Name(trip.State),
                trip.FareMinor,
                trip.Currency,
                AwaitingDriver: true,
                offered.DriversOffered);
        }

        private async Task<Trip> AdmitAsync(Request request, CancellationToken ct)
        {
            QuotePayload quote;
            try
            {
                quote = tokens.Decode(request.QuoteToken);
            }
            catch (InvalidQuoteTokenException)
            {
                throw Refused("that quote is unknown or has been altered", "unknown_quote");
            }

            var now = clock.Now;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            if (quote.ExpiresAt <= now)
            {
                throw Refused("that quote has expired", "expired_quote");
            }

            var trip = new Trip
            {
                Id = ids.Create(),
                RiderId = request.RiderId,
                State = TripState.Requested,
                Version = 1,
                FareMinor = quote.TotalMinor,
                Currency = quote.Currency,
                QuoteId = quote.QuoteId,
                QuoteToken = request.QuoteToken,
                Pickup = quote.Pickup,
                Dropoff = quote.Dropoff,
                CreatedAt = now,
            };

            try
            {
                db.Trips.Add(trip);
                db.TripEvents.Add(new TripEventOutbox
                {
                    EventId = Guid.NewGuid(),
                    TripId = trip.Id,
                    Version = trip.Version,
                    State = trip.State,
                    QuoteToken = trip.QuoteToken,
                    PaymentMethod = "default",
                    OccurredAt = now,
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return trip;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" } violation)
            {
                await transaction.RollbackAsync(ct);
                throw violation.ConstraintName switch
                {
                    "ux_trip_rider_active" =>
                        Refused("that rider already holds an active trip", "rider_has_active_trip"),
                    _ => Refused("that quote has already been spent", "quote_already_consumed"),
                };
            }
        }

        private static BusinessRuleException Refused(string message, string reason) =>
            new(message, $"trip:request:create:{reason}");
    }
}
