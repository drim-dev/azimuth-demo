using Azimuth.Annotations;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Dispatch;

/// <summary>
/// Offers a requested trip to the available drivers near its pickup, and to no others.
/// </summary>
/// <remarks>
/// Reached from the admission slice rather than mapped to a route: a trip is fanned out because it
/// was admitted, and there is no caller who may fan one out on its own.
/// </remarks>
public static class OfferTripToDrivers
{
    private static readonly TimeSpan Validity = TimeSpan.FromSeconds(30);

    public sealed record Request(long TripId, string Near) : IRequest<Response>;

    public sealed record Response(int DriversOffered);

    public sealed class RequestHandler(TripDbContext db, Clock clock) : IRequestHandler<Request, Response>
    {
        [Realizes("trip/dispatch", "offer-sent-to-available-nearby-driver")]
        [Realizes("trip/dispatch", "unavailable-driver-not-offered")]
        [Realizes("trip/dispatch", "no-available-drivers")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var alreadyOffered = await db.Offers
                .AsNoTracking()
                .Where(o => o.TripId == request.TripId)
                .Select(o => o.DriverId)
                .ToListAsync(ct);

            var drivers = await db.Drivers
                .AsNoTracking()
                .Where(d => d.Available && d.Near == request.Near)
                .Where(d => !alreadyOffered.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync(ct);

            if (drivers.Count == 0)
            {
                return new Response(0);
            }

            var now = clock.Now;
            db.Offers.AddRange(drivers.Select(driverId => new Offer
            {
                TripId = request.TripId,
                DriverId = driverId,
                State = OfferState.Offered,
                OfferedAt = now,
                ExpiresAt = now + Validity,
            }));

            await db.SaveChangesAsync(ct);
            return new Response(drivers.Count);
        }
    }
}
