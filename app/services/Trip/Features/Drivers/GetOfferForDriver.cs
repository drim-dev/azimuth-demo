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

/// <summary>What a driver sees before accepting: the pickup area and the fare, and no rider.</summary>
public static class GetOfferForDriver
{
    private const string PickupArea = "downtown";

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/drivers/{driverId}/offers/{id}", async (
                string driverId,
                string id,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(driverId, id), ct)));
    }

    public sealed record Request(string DriverId, string Id) : IRequest<DriverOfferView>;

    public sealed class RequestHandler(TripDbContext db) : IRequestHandler<Request, DriverOfferView>
    {
        [Realizes("trip/driver-view", "pickup-shown-on-offer")]
        [Realizes("trip/driver-view", "rider-contact-hidden-on-offer")]
        [Realizes("trip/driver-view", "rider-contact-confined-to-held-trips")]
        public async Task<DriverOfferView> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw NotFound();
            }

            var trip = await db.Trips
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new { t.Id, t.FareMinor, t.Currency })
                .FirstOrDefaultAsync(ct);

            if (trip is null)
            {
                throw NotFound();
            }

            return DriverProjection.Offer(
                IdEncoding.Encode(trip.Id),
                PickupArea,
                Money.Of(trip.FareMinor, trip.Currency));
        }

        private static NotFoundException NotFound() =>
            new("no such offer", "trip:driver:get_offer:not_found");
    }
}
