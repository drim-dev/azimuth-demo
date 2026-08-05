using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pricing;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Drivers;

/// <summary>The driver's view of a trip, which reveals a rider contact only while they hold it.</summary>
public static class GetTripForDriver
{
    private const string PickupArea = "downtown";

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/drivers/{driverId}/trips/{id}", async (
                string driverId,
                string id,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(driverId, id), ct)));
    }

    public sealed record Request(string DriverId, string Id) : IRequest<DriverTripView>;

    public sealed class RequestHandler(TripDbContext db) : IRequestHandler<Request, DriverTripView>
    {
        [Realizes("trip/driver-view", "proxy-contact-while-held")]
        [Realizes("trip/driver-view", "contact-withdrawn-after-terminal")]
        [Realizes("trip/driver-view", "rider-contact-hidden-on-offer")]
        [Realizes("trip/driver-view", "rider-contact-confined-to-held-trips")]
        public async Task<DriverTripView> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw new NotFoundException("no such trip", "trip:driver:get_trip:not_found");
            }

            var trip = await db.Trips
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new { t.Id, t.State, t.FareMinor, t.Currency, t.RiderId, t.AssignedDriverId })
                .FirstOrDefaultAsync(ct);

            if (trip is null)
            {
                throw new NotFoundException("no such trip", "trip:driver:get_trip:not_found");
            }

            return DriverProjection.For(
                IdEncoding.Encode(trip.Id),
                trip.State,
                PickupArea,
                Money.Of(trip.FareMinor, trip.Currency),
                heldByThisDriver: trip.AssignedDriverId == request.DriverId,
                RiderContact.Of($"proxy:{trip.RiderId}"));
        }
    }
}
