using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Dispatch;

public static class GetOffers
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/trips/{id}/offers", async (string id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(id), ct)));
    }

    public sealed record Request(string Id) : IRequest<IReadOnlyList<Offered>>;

    public sealed record Offered(string DriverId, string State);

    public sealed class RequestHandler(TripDbContext db, Clock clock)
        : IRequestHandler<Request, IReadOnlyList<Offered>>
    {
        /// <summary>Withdraws what has lapsed before reporting, so a lapsed offer is never shown live.</summary>
        [Realizes("trips/dispatch", "offer-sent-to-available-nearby-driver")]
        [Realizes("trips/dispatch", "unavailable-driver-not-offered")]
        [Realizes("trips/dispatch", "no-available-drivers")]
        [Realizes("trips/dispatch", "other-offers-withdrawn")]
        [Realizes("trips/dispatch", "expired-offer-withdrawn")]
        public async Task<IReadOnlyList<Offered>> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw new NotFoundException("no such trip", "trip:dispatch:list_offers:not_found");
            }

            var now = clock.Now;
            await db.Offers
                .Where(o => o.State == OfferState.Offered && o.ExpiresAt <= now)
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.State, OfferState.Withdrawn), ct);

            var offers = await db.Offers
                .AsNoTracking()
                .Where(o => o.TripId == id)
                .OrderBy(o => o.DriverId)
                .Select(o => new { o.DriverId, o.State })
                .ToListAsync(ct);

            return offers.Select(o => new Offered(o.DriverId, OfferStateNames.Of(o.State))).ToList();
        }
    }
}
