using System.Globalization;
using System.Text;
using Common.Http;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Database;
using Payments.Features.Captures.Options;
using Payments.Features.Events;

namespace Payments.Features.Captures;

public static class GetCaptureSettlementMetrics
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/operations/metrics", async (ISender sender, CancellationToken ct) =>
                Results.Text(
                    await sender.Send(new Request(), ct),
                    "text/plain; version=0.0.4; charset=utf-8"));
    }

    public sealed record Request : IRequest<string>;

    public sealed class RequestHandler(
        PaymentsDbContext db,
        Clock clock,
        IOptions<CaptureSettlementOptions> options,
        CaptureSettlementState captureState,
        PaymentEventRelayState eventState) : IRequestHandler<Request, string>
    {
        public async Task<string> Handle(Request request, CancellationToken ct)
        {
            var now = clock.Now;
            var pending = await db.CaptureIntents
                .AsNoTracking()
                .Where(item => item.DispatchedAt == null)
                .Select(item => item.WrittenAt)
                .ToListAsync(ct);
            var overdue = pending.Count(writtenAt => now - writtenAt >= options.Value.OverdueAfter);
            var oldestAge = pending.Count == 0
                ? 0
                : Math.Max(0, (now - pending.Min()).TotalSeconds);
            var captureLastSuccess = captureState.LastSuccess?.ToUnixTimeSeconds() ?? 0;
            var pendingEvents = await db.PaymentEvents
                .AsNoTracking()
                .Where(item => item.PublishedAt == null)
                .Select(item => item.OccurredAt)
                .ToListAsync(ct);
            var oldestEventAge = pendingEvents.Count == 0
                ? 0
                : Math.Max(0, (now - pendingEvents.Min()).TotalSeconds);
            var eventLastSuccess = eventState.LastSuccess?.ToUnixTimeSeconds() ?? 0;

            var output = new StringBuilder();
            Gauge(output, "payments_capture_pending_intents", pending.Count);
            Gauge(output, "payments_capture_overdue_intents", overdue);
            Gauge(output, "payments_capture_oldest_pending_age_seconds", oldestAge);
            Gauge(output, "payments_capture_worker_last_success_timestamp_seconds", captureLastSuccess);
            Gauge(output, "payments_event_outbox_pending", pendingEvents.Count);
            Gauge(output, "payments_event_outbox_oldest_pending_age_seconds", oldestEventAge);
            Gauge(output, "payments_event_relay_last_success_timestamp_seconds", eventLastSuccess);
            return output.ToString();
        }

        private static void Gauge(StringBuilder output, string name, double value)
        {
            output.Append("# TYPE ").Append(name).AppendLine(" gauge");
            output.Append(name).Append(' ')
                .AppendLine(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
