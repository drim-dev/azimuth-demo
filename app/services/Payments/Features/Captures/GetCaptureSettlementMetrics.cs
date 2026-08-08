using System.Globalization;
using System.Text;
using Azimuth.Annotations;
using Common.Http;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Database;
using Payments.Features.Captures.Options;

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
        CaptureSettlementState state) : IRequestHandler<Request, string>
    {
        [Realizes("payments/capture", "capture-created-on-completion")]
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
            var lastSuccess = state.LastSuccess?.ToUnixTimeSeconds() ?? 0;

            var output = new StringBuilder();
            Gauge(output, "payments_capture_pending_intents", pending.Count);
            Gauge(output, "payments_capture_overdue_intents", overdue);
            Gauge(output, "payments_capture_oldest_pending_age_seconds", oldestAge);
            Gauge(output, "payments_capture_worker_last_success_timestamp_seconds", lastSuccess);
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
