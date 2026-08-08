using Analytics.Database;
using Common.Http;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Features.TripActivity;

public static class GetTripActivitySummary
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/activity/summary", async (ISender sender, CancellationToken cancellation) =>
                Results.Ok(await sender.Send(new Request(), cancellation)));
    }

    public sealed record Request : IRequest<Response>;

    public sealed record StateCount(string State, int Trips);

    public sealed record Response(int TotalTrips, IReadOnlyList<StateCount> States);

    public sealed class RequestHandler(AnalyticsDbContext db) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken cancellation)
        {
            var rows = await db.TripActivity.AsNoTracking()
                .GroupBy(x => x.State)
                .Select(group => new { State = group.Key, Trips = group.Count() })
                .OrderBy(x => x.State)
                .ToListAsync(cancellation);
            var counts = rows.Select(x => new StateCount(x.State, x.Trips)).ToList();
            return new Response(counts.Sum(x => x.Trips), counts);
        }
    }
}
