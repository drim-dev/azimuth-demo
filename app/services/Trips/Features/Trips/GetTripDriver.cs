using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;

namespace Trips.Features.Trips;

/// <summary>
/// The assigned driver's display record for a trip. Carries no position.
/// </summary>
/// <remarks>
/// This route was written to give the receipt's map thumbnail coordinates, by reaching past the
/// projection for the stored record. The invariant caught it: a rider-facing site that hands out a
/// raw position discharges nothing, whatever phase the trip is in.
/// <para>
/// The thumbnail uses the trip's pickup and dropoff, which are the rider's own data. The driver's
/// position was never the right source for it.
/// </para>
/// </remarks>
public static class GetTripDriver
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/trips/{id}/driver", async (string id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(id), ct)));
    }

    public sealed record Request(string Id) : IRequest<Response>;

    public sealed record Response(string Id, string Display, string Vehicle);

    public sealed class RequestHandler(TripDbContext db) : IRequestHandler<Request, Response>
    {
        [Realizes("trips/rider-view", "driver-identity-remains-on-receipt")]
        [Realizes("trips/rider-view", "position-confined-to-live-phases")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw NotFound();
            }

            var assignedDriverId = await db.Trips
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => t.AssignedDriverId)
                .FirstOrDefaultAsync(ct);

            if (assignedDriverId is null)
            {
                throw NotFound();
            }

            var driver = await db.Drivers
                .AsNoTracking()
                .Where(d => d.Id == assignedDriverId)
                .Select(d => new Response(d.Id, d.Display, d.Vehicle))
                .FirstOrDefaultAsync(ct);

            return driver ?? throw NotFound();
        }

        private static NotFoundException NotFound() =>
            new("no such trip", "trip:trip:get_driver:not_found");
    }
}
