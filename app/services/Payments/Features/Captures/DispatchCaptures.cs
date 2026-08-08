using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

/// <summary>
/// Drains the outbox: captures every undispatched intent, at most once per trip.
/// </summary>
/// <remarks>
/// The dispatcher is the only reader of <c>capture_intents</c>, which the trip service writes in
/// the same transaction as the completion. A transactional outbox rather than a direct call:
/// calling the payment client inline from the completion handler is the single most-repeated
/// mistake in the concern catalog (C16) — it charges riders for transactions that roll back, and no
/// behavioural test catches it because the failing case needs a rollback at one exact instant.
/// </remarks>
public static class DispatchCaptures
{
    public sealed class Endpoint : IEndpoint
    {
        /// <summary>
        /// The reason travels on the request because an adjustment is a decision the caller makes,
        /// not a property of the intent. It reaches the wire because a capability only a handler
        /// call can exercise is one no client can use, and the claim asserts the product has it.
        /// </summary>
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/dispatch", async (
                string? adjustmentReason,
                long? adjustmentMinor,
                ISender sender,
                CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(adjustmentReason, adjustmentMinor ?? 0), ct)));
    }

    public sealed record Request(string? AdjustmentReason = null, long AdjustmentMinor = 0)
        : IRequest<Response>;

    public sealed record Response(int Captured, int Quarantined, int Deferred);

    public sealed class RequestHandler(
        PaymentsDbContext db,
        ISender sender,
        Clock clock,
        ILogger<RequestHandler> logger) : IRequestHandler<Request, Response>
    {
        [Realizes("payments/capture", "capture-created-on-completion")]
        [Realizes("payments/capture", "duplicate-completion-event")]
        [Realizes("payments/capture", "concurrent-completion-processing")]
        [Realizes("payments/capture", "retry-after-transport-failure")]
        [Realizes("payments/capture", "capture-equals-trip-fare")]
        [Realizes("payments/capture", "adjusted-capture-records-reason")]
        [Realizes("payments/capture", "declined-capture-recorded")]
        [Realizes("payments/capture", "declined-capture-is-retryable")]
        [Realizes("payments/capture", "malformed-intent-does-not-starve-batch")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var pending = await db.CaptureIntents
                .AsNoTracking()
                .Where(i => i.DispatchedAt == null)
                .OrderBy(i => i.WrittenAt)
                .ThenBy(i => i.TripId)
                .Select(i => new { i.TripId, i.QuoteToken, i.PaymentMethod })
                .ToListAsync(ct);

            var captured = 0;
            var quarantined = 0;
            var deferred = 0;
            foreach (var intent in pending)
            {
                try
                {
                    var result = await sender.Send(
                        new CaptureTrip.Request(
                            intent.TripId,
                            intent.QuoteToken,
                            intent.PaymentMethod,
                            request.AdjustmentReason,
                            request.AdjustmentMinor),
                        ct);

                    if (result.Captured)
                    {
                        captured++;
                    }
                }
                catch (BusinessRuleException exception)
                    when (exception.ErrorCode == "payment:capture:create:invalid_quote")
                {
                    await Quarantine(intent.TripId, exception.ErrorCode, ct);
                    quarantined++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Transient failures remain pending, but one provider or infrastructure fault
                    // must not prevent independent intents later in this batch from being tried.
                    logger.LogError(exception, "Capture for trip {TripId} was deferred", intent.TripId);
                    deferred++;
                }
            }

            return new Response(captured, quarantined, deferred);
        }

        private async Task Quarantine(long tripId, string reason, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.CaptureFailures.Add(new Domain.CaptureFailure
            {
                TripId = tripId,
                Reason = reason,
                OccurredAt = clock.Now,
            });
            await db.SaveChangesAsync(ct);
            await db.CaptureIntents
                .Where(intent => intent.TripId == tripId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(intent => intent.DispatchedAt, clock.Now),
                    ct);
            await transaction.CommitAsync(ct);
        }
    }
}
