using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Referrals;
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

    public sealed record Request(
        string RiderId,
        string QuoteToken,
        string? ReferralCode = null,
        string? ReferralCreditId = null) : IRequest<Response>;

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
            RuleFor(x => x.ReferralCode).NotEmpty().MaximumLength(64)
                .When(x => x.ReferralCode is not null);
            RuleFor(x => x.ReferralCreditId).NotEmpty().MaximumLength(32)
                .When(x => x.ReferralCreditId is not null);
        }
    }

    public sealed class RequestHandler(
        TripDbContext db,
        ISender sender,
        IdFactory ids,
        Clock clock,
        QuoteTokenCodec tokens,
        ReferralCreditAuthorityCodec referralAuthorities)
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
        [Realizes("referrals/rewards", "known-code-is-attributed")]
        [Realizes("referrals/rewards", "unknown-code-is-rejected")]
        [Realizes("referrals/rewards", "self-referral-is-rejected")]
        [Realizes("referrals/rewards", "attribution-cannot-be-replaced")]
        [Realizes("referrals/rewards", "unavailable-credit-is-rejected")]
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

            var tripId = ids.Create();
            var firstAdmission = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO rider_admissions (rider_id, first_trip_id)
                VALUES ({request.RiderId}, {tripId})
                ON CONFLICT (rider_id) DO NOTHING
                """, ct) == 1;

            ReferralAttribution? attribution = null;
            if (request.ReferralCode is not null)
            {
                if (!firstAdmission)
                {
                    throw Refused(
                        "referral attribution is available only before the first admitted trip",
                        "referral_eligibility_closed");
                }

                var referrer = await db.ReferralAccounts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Code == request.ReferralCode, ct);
                if (referrer is null)
                {
                    throw Refused("that referral code is unknown", "unknown_referral_code");
                }

                if (referrer.RiderId == request.RiderId)
                {
                    throw Refused("a rider cannot refer themselves", "self_referral");
                }

                attribution = new ReferralAttribution
                {
                    Id = ids.Create(),
                    ReferredRiderId = request.RiderId,
                    ReferrerRiderId = referrer.RiderId,
                    FirstTripId = tripId,
                };
            }

            ReferralCredit? credit = null;
            if (request.ReferralCreditId is not null)
            {
                if (!IdEncoding.TryDecode(request.ReferralCreditId, out var creditId))
                {
                    throw CreditRefused("that referral credit is unknown", "unknown_credit");
                }

                credit = await db.ReferralCredits
                    .FromSql($"SELECT * FROM referral_credits WHERE id = {creditId} FOR UPDATE")
                    .SingleOrDefaultAsync(ct);
                if (credit is null)
                {
                    throw CreditRefused("that referral credit is unknown", "unknown_credit");
                }

                if (credit.BeneficiaryRiderId != request.RiderId)
                {
                    throw CreditRefused("that referral credit belongs to another rider", "foreign_credit");
                }

                if (credit.State != ReferralCreditState.Available)
                {
                    throw CreditRefused("that referral credit is not available", "unavailable_credit");
                }

                if (!string.Equals(credit.Currency, quote.Currency, StringComparison.Ordinal))
                {
                    throw CreditRefused("that referral credit uses another currency", "wrong_currency");
                }

                if (credit.AmountMinor > quote.TotalMinor)
                {
                    throw CreditRefused("that referral credit exceeds the fare", "credit_exceeds_fare");
                }

                credit.State = ReferralCreditState.Reserved;
                credit.ReservedTripId = tripId;
            }

            var authority = credit is null
                ? null
                : referralAuthorities.Encode(new ReferralCreditAuthority(
                    credit.Id,
                    tripId,
                    credit.AmountMinor,
                    credit.Currency));

            var trip = new Trip
            {
                Id = tripId,
                RiderId = request.RiderId,
                State = TripState.Requested,
                Version = 1,
                FareMinor = quote.TotalMinor,
                Currency = quote.Currency,
                QuoteId = quote.QuoteId,
                QuoteToken = request.QuoteToken,
                ReferralCreditId = credit?.Id,
                ReferralCreditAuthority = authority,
                Pickup = quote.Pickup,
                Dropoff = quote.Dropoff,
                CreatedAt = now,
            };

            try
            {
                db.Trips.Add(trip);
                if (attribution is not null)
                {
                    db.ReferralAttributions.Add(attribution);
                }
                db.TripEvents.Add(new TripEventOutbox
                {
                    EventId = Guid.NewGuid(),
                    TripId = trip.Id,
                    Version = trip.Version,
                    State = trip.State,
                    QuoteToken = trip.QuoteToken,
                    PaymentMethod = "default",
                    ReferralCreditAuthority = authority,
                    OccurredAt = now,
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return trip;
            }
            catch (DbUpdateException e) when (
                e.InnerException is PostgresException { SqlState: "23505" } violation
                && violation.ConstraintName is "ux_trip_rider_active"
                    or "ux_trip_quote"
                    or "ux_referral_attribution_referred_rider")
            {
                await transaction.RollbackAsync(ct);
                throw violation.ConstraintName switch
                {
                    "ux_trip_rider_active" =>
                        Refused("that rider already holds an active trip", "rider_has_active_trip"),
                    "ux_trip_quote" => Refused(
                        "that quote has already been spent",
                        "quote_already_consumed"),
                    "ux_referral_attribution_referred_rider" => Refused(
                        "referral attribution cannot be replaced",
                        "referral_eligibility_closed"),
                    _ => throw new InvalidOperationException("unrecognized guarded constraint"),
                };
            }
        }

        private static BusinessRuleException Refused(string message, string reason) =>
            new(message, $"trip:request:create:{reason}");

        private static BusinessRuleException CreditRefused(string message, string reason) =>
            new(message, $"trip:referral:reserve:{reason}");
    }
}
