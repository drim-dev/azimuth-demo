using Azimuth.Annotations;
using Common.Http;
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

    public sealed record Response(int Captured);

    public sealed class RequestHandler(PaymentsDbContext db, ISender sender) : IRequestHandler<Request, Response>
    {
        [Realizes("payments/capture", "capture-created-on-completion")]
        [Realizes("payments/capture", "duplicate-completion-event")]
        [Realizes("payments/capture", "concurrent-completion-processing")]
        [Realizes("payments/capture", "retry-after-transport-failure")]
        [Realizes("payments/capture", "capture-equals-trip-fare")]
        [Realizes("payments/capture", "adjusted-capture-records-reason")]
        [Realizes("payments/capture", "declined-capture-recorded")]
        [Realizes("payments/capture", "declined-capture-is-retryable")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var pending = await db.CaptureIntents
                .AsNoTracking()
                .Where(i => i.DispatchedAt == null)
                .Select(i => new { i.TripId, i.QuoteToken })
                .ToListAsync(ct);

            var captured = 0;
            foreach (var intent in pending)
            {
                var result = await sender.Send(
                    new CaptureTrip.Request(
                        intent.TripId,
                        intent.QuoteToken,
                        request.AdjustmentReason,
                        request.AdjustmentMinor),
                    ct);

                if (result.Captured)
                {
                    captured++;
                }
            }

            return new Response(captured);
        }
    }
}
