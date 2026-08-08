using Azimuth.Annotations;
using Common.Exceptions;
using Common.Identity;
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
        string? AdjustmentReason = null,
        long AdjustmentMinor = 0)
        : IRequest<Response>;

    public sealed record Response(bool Captured);

    public sealed class RequestHandler(
        PaymentsDbContext db,
        IPaymentProvider provider,
        IdFactory ids,
        Clock clock,
        QuoteTokenCodec tokens)
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

            long captureAmount;
            try
            {
                captureAmount = checked(quote.TotalMinor + request.AdjustmentMinor);
            }
            catch (OverflowException)
            {
                throw new BusinessRuleException(
                    "the adjusted capture amount exceeds minor-unit bounds",
                    "payment:capture:create:invalid_adjustment");
            }

            if (captureAmount < 0
                || (request.AdjustmentMinor != 0 && request.AdjustmentReason is null))
            {
                throw new BusinessRuleException(
                    "an adjustment requires a reason and cannot make the capture negative",
                    "payment:capture:create:invalid_adjustment");
            }

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

            db.Captures.Add(new Capture
            {
                Id = ids.Create(),
                TripId = request.TripId,
                AmountMinor = captureAmount,
                Currency = quote.Currency,
                AdjustmentReason = request.AdjustmentReason,
                CapturedAt = clock.Now,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
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
    }
}
