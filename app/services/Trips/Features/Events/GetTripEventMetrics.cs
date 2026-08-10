using System.Globalization;
using System.Text;
using Common.Http;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;

namespace Trips.Features.Events;

public static class GetTripEventMetrics
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/operations/trip-events/metrics", async (
                ISender sender,
                CancellationToken cancellation) =>
                Results.Text(
                    await sender.Send(new Request(), cancellation),
                    "text/plain; version=0.0.4; charset=utf-8"));
    }

    public sealed record Request : IRequest<string>;

    public sealed class RequestHandler(
        TripDbContext db,
        Clock clock,
        TripEventRelayState state) : IRequestHandler<Request, string>
    {
        public async Task<string> Handle(Request request, CancellationToken cancellation)
        {
            var pending = await db.TripEvents.AsNoTracking()
                .Where(x => x.PublishedAt == null)
                .Select(x => x.OccurredAt)
                .ToListAsync(cancellation);
            var oldestAge = pending.Count == 0
                ? 0
                : Math.Max(0, (clock.Now - pending.Min()).TotalSeconds);

            var output = new StringBuilder();
            Gauge(output, "trips_event_outbox_pending", pending.Count);
            Gauge(output, "trips_event_outbox_oldest_pending_age_seconds", oldestAge);
            Gauge(output, "trips_event_relay_last_success_timestamp_seconds",
                state.LastSuccess?.ToUnixTimeSeconds() ?? 0);
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
