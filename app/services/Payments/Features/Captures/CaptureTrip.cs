using Azimuth.Annotations;
using Common.Exceptions;
using Common.Identity;
using Common.Referrals;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Payments.Database;
using Payments.Domain;
using Pricing;

namespace Payments.Features.Captures;

/// <summary>Turns one completed trip into exactly one charge. Safe to send concurrently and repeatedly.</summary>
public static class CaptureTrip
{
    public sealed record Request(
        long TripId,
        string QuoteToken,
        string PaymentMethod,
        string? ReferralCreditAuthority = null)
        : IRequest<Response>;

    public sealed record Response(bool Captured);

    public sealed class RequestHandler(
        PaymentsDbContext db,
        IPaymentProvider provider,
        IdFactory ids,
        Clock clock,
        QuoteTokenCodec tokens,
        ReferralCreditAuthorityCodec referralCredits)
        : IRequestHandler<Request, Response>
    {
        /// <summary>
        /// Captures one trip, at most once.
        /// </summary>
        /// <remarks>
        /// The unique index is what holds under concurrency. The pre-check reads well and produces
        /// the answer a caller wants; it is not what makes the claim true.
        /// </remarks>
        [Realizes("payments/capture", "duplicate-completion-event")]
        [Realizes("payments/capture", "concurrent-completion-processing")]
        [Realizes("payments/capture", "retry-after-transport-failure")]
        [Realizes("payments/capture", "capture-equals-trip-fare")]
        [Realizes("payments/capture", "adjusted-capture-records-reason")]
        [Realizes("payments/capture", "declined-capture-recorded")]
        [Realizes("payments/capture", "declined-capture-is-retryable")]
        [Realizes("referrals/rewards", "owned-credit-reduces-capture")]
        [Realizes("referrals/rewards", "forged-credit-authority-is-rejected")]
        [Realizes("referrals/rewards", "capture-redelivery-does-not-redeem-twice")]
        [Realizes("payments/capture", "committed-capture-is-published")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            QuotePayload quote;
            try
            {
                quote = tokens.Decode(request.QuoteToken);
            }
            catch (InvalidQuoteTokenException exception)
            {
                throw new BusinessRuleException(
                    exception.Message,
                    "payment:capture:create:invalid_quote");
            }

            var alreadyCaptured = await db.Captures
                .AsNoTracking()
                .AnyAsync(c => c.TripId == request.TripId && !c.Voided, ct);

            if (alreadyCaptured)
            {
                return new Response(Captured: false);
            }

            var referralCredit = DecodeReferralCredit(request, quote);
            if (referralCredit is not null)
            {
                var priorTrip = await db.Captures
                    .AsNoTracking()
                    .Where(c => c.ReferralCreditId == referralCredit.CreditId)
                    .Select(c => (long?)c.TripId)
                    .SingleOrDefaultAsync(ct);
                if (priorTrip is not null && priorTrip != request.TripId)
                {
                    throw InvalidReferralCredit("that referral credit already adjusted another trip");
                }
            }

            var referralCreditMinor = referralCredit?.AmountMinor ?? 0;
            var captureAmount = quote.TotalMinor - referralCreditMinor;

            var outcome = await provider.CaptureAsync(
                request.TripId,
                captureAmount,
                quote.Currency,
                request.PaymentMethod);

            // An outcome the caller never observed may or may not have succeeded, so it is treated
            // as possibly-captured and the index settles it. Assuming failure here double-charges.
            if (outcome == ProviderOutcome.Declined)
            {
                db.CaptureFailures.Add(new CaptureFailure
                {
                    TripId = request.TripId,
                    Reason = "declined",
                    OccurredAt = clock.Now,
                });

                await db.SaveChangesAsync(ct);
                await db.CaptureIntents
                    .Where(intent => intent.TripId == request.TripId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(intent => intent.DispatchedAt, clock.Now),
                        ct);
                return new Response(Captured: false);
            }

            var captureId = ids.Create();
            var capturedAt = clock.Now;
            db.Captures.Add(new Capture
            {
                Id = captureId,
                TripId = request.TripId,
                OriginalFareMinor = quote.TotalMinor,
                ReferralCreditMinor = referralCreditMinor,
                ReferralCreditId = referralCredit?.CreditId,
                AmountMinor = captureAmount,
                Currency = quote.Currency,
                AdjustmentReason = referralCredit is null ? null : "referral-credit",
                CapturedAt = capturedAt,
            });
            db.PaymentEvents.Add(new PaymentEventOutbox
            {
                EventId = Guid.NewGuid(),
                CaptureId = captureId,
                TripId = request.TripId,
                OriginalFareMinor = quote.TotalMinor,
                ReferralCreditMinor = referralCreditMinor,
                CapturedAmountMinor = captureAmount,
                Currency = quote.Currency,
                ReferralCreditId = referralCredit?.CreditId,
                OccurredAt = capturedAt,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
                { SqlState: "23505", ConstraintName: "ux_capture_referral_credit" })
            {
                db.ChangeTracker.Clear();
                var winnerTrip = await db.Captures
                    .AsNoTracking()
                    .Where(c => c.ReferralCreditId == referralCredit!.CreditId)
                    .Select(c => c.TripId)
                    .SingleAsync(ct);
                if (winnerTrip == request.TripId)
                {
                    return new Response(Captured: false);
                }
                throw InvalidReferralCredit("that referral credit already adjusted another trip");
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
                { SqlState: "23505", ConstraintName: "ux_capture_trip" })
            {
                // Another worker won. Exactly the case the pre-check cannot cover.
                db.ChangeTracker.Clear();
                return new Response(Captured: false);
            }

            await db.CaptureIntents
                .Where(i => i.TripId == request.TripId)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.DispatchedAt, clock.Now), ct);

            return new Response(Captured: true);
        }

        private ReferralCreditAuthority? DecodeReferralCredit(Request request, QuotePayload quote)
        {
            if (request.ReferralCreditAuthority is null)
            {
                return null;
            }

            ReferralCreditAuthority authority;
            try
            {
                authority = referralCredits.Decode(request.ReferralCreditAuthority);
            }
            catch (InvalidReferralCreditAuthorityException)
            {
                throw InvalidReferralCredit("that referral credit authority is invalid");
            }

            if (authority.TripId != request.TripId
                || !string.Equals(authority.Currency, quote.Currency, StringComparison.Ordinal)
                || authority.AmountMinor <= 0
                || authority.AmountMinor > quote.TotalMinor)
            {
                throw InvalidReferralCredit("that referral credit authority does not match the trip");
            }

            return authority;
        }

        private static BusinessRuleException InvalidReferralCredit(string message) =>
            new(message, "payment:capture:create:invalid_referral_credit");
    }
}
