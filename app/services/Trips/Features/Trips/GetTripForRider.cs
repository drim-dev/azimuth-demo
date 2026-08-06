using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pricing;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Trips;

/// <summary>
/// The rider's view of a trip. Everything the rider may see about a driver passes through
/// <see cref="RiderProjection.For"/>; nothing here decides visibility.
/// </summary>
public static class GetTripForRider
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/trips/{id}", async (string id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(id), ct)));
    }

    public sealed record Request(string Id) : IRequest<RiderTripView>;

    public sealed class RequestHandler(TripDbContext db) : IRequestHandler<Request, RiderTripView>
    {
        [Realizes("trips/rider-view", "no-driver-identity-before-assignment")]
        [Realizes("trips/rider-view", "no-driver-position-before-assignment")]
        [Realizes("trips/rider-view", "supply-density-shown-before-assignment")]
        [Realizes("trips/rider-view", "driver-shown-after-assignment")]
        [Realizes("trips/rider-view", "driver-position-follows-driver")]
        [Realizes("trips/rider-view", "no-position-after-completion")]
        [Realizes("trips/rider-view", "no-position-after-cancellation")]
        [Realizes("trips/rider-view", "driver-identity-remains-on-receipt")]
        [Realizes("trips/rider-view", "position-confined-to-live-phases")]
        public async Task<RiderTripView> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw NotFound();
            }

            var trip = await db.Trips
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new { t.Id, t.State, t.FareMinor, t.Currency, t.AssignedDriverId })
                .FirstOrDefaultAsync(ct);

            if (trip is null)
            {
                throw NotFound();
            }

            var driver = trip.AssignedDriverId is null
                ? null
                : await db.Drivers
                    .AsNoTracking()
                    .Where(d => d.Id == trip.AssignedDriverId)
                    .Select(d => new { d.Display, d.Vehicle, d.Position })
                    .FirstOrDefaultAsync(ct);

            return RiderProjection.For(
                IdEncoding.Encode(trip.Id),
                trip.State,
                Money.Of(trip.FareMinor, trip.Currency),
                driver?.Display,
                driver?.Vehicle,
                DriverPosition.From(driver?.Position),
                supplyDensity: "moderate");
        }

        private static NotFoundException NotFound() =>
            new("no such trip", "trip:trip:get:not_found");
    }
}
