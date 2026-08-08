using Analytics.Database;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Features.TripActivity;

public static class GetTripActivity
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/activity/trips/{tripId}", async (
                string tripId,
                ISender sender,
                CancellationToken cancellation) =>
            {
                if (!IdEncoding.TryDecode(tripId, out var decoded))
                {
                    return Results.NotFound();
                }

                var response = await sender.Send(new Request(decoded), cancellation);
                return response is null ? Results.NotFound() : Results.Ok(response);
            });
    }

    public sealed record Request(long TripId) : IRequest<Response?>;

    public sealed record Response(long TripId, long Version, string State, DateTimeOffset OccurredAt);

    public sealed class RequestHandler(AnalyticsDbContext db) : IRequestHandler<Request, Response?>
    {
        public Task<Response?> Handle(Request request, CancellationToken cancellation) =>
            db.TripActivity.AsNoTracking()
                .Where(x => x.TripId == request.TripId)
                .Select(x => new Response(x.TripId, x.Version, x.State, x.OccurredAt))
                .SingleOrDefaultAsync(cancellation);
    }
}
