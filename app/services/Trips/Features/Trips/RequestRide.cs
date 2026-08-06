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

    public sealed record Request(string RiderId, string QuoteId) : IRequest<Response>;

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
            RuleFor(x => x.QuoteId).NotEmpty();
        }
    }

    public sealed class RequestHandler(TripDbContext db, ISender sender, IdFactory ids, Clock clock)
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
            if (!IdEncoding.TryDecode(request.QuoteId, out var quoteId))
            {
                throw Refused("no such quote", "unknown_quote");
            }

            var now = clock.Now;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var quote = await db.Quotes
                .AsNoTracking()
                .Where(q => q.Id == quoteId)
                .Select(q => new { q.TotalMinor, q.Currency, q.ExpiresAt, q.ConsumedByTripId })
                .FirstOrDefaultAsync(ct);

            if (quote is null)
            {
                throw Refused("no such quote", "unknown_quote");
            }

            if (quote.ExpiresAt <= now)
            {
                throw Refused("that quote has expired", "expired_quote");
            }

            if (quote.ConsumedByTripId is not null)
            {
                throw Refused("that quote has already been spent", "quote_already_consumed");
            }

            var trip = new Trip
            {
                Id = ids.Create(),
                RiderId = request.RiderId,
                State = TripState.Requested,
                FareMinor = quote.TotalMinor,
                Currency = quote.Currency,
                QuoteId = quoteId,
                CreatedAt = now,
            };

            try
            {
                db.Trips.Add(trip);
                await db.SaveChangesAsync(ct);

                // Conditional, so a quote spent between the read above and here is refused rather
                // than double-spent. The affected-row count is the answer; re-reading would race.
                var consumed = await db.Quotes
                    .Where(q => q.Id == quoteId && q.ConsumedByTripId == null)
                    .ExecuteUpdateAsync(q => q.SetProperty(x => x.ConsumedByTripId, trip.Id), ct);

                if (consumed != 1)
                {
                    await transaction.RollbackAsync(ct);
                    throw Refused("that quote has already been spent", "quote_already_consumed");
                }

                await transaction.CommitAsync(ct);
                return trip;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" } violation)
            {
                await transaction.RollbackAsync(ct);
                throw violation.ConstraintName == "ux_trip_rider_active"
                    ? Refused("that rider already holds an active trip", "rider_has_active_trip")
                    : Refused("that quote has already been spent", "quote_already_consumed");
            }
        }

        private static BusinessRuleException Refused(string message, string reason) =>
            new(message, $"trip:request:create:{reason}");
    }
}
