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

/// <summary>
/// Settles acceptance by compare-and-set on the trip's assignment.
/// </summary>
/// <remarks>
/// Not a check-then-write in the handler: two accepts arriving together both read null, and only
/// the update matters. The losing driver's answer comes from the affected-row count rather than
/// from re-reading, so there is no window in which a loser is told it won.
/// <para>
/// A distributed lock over the trip was rejected — it moves the correctness argument into the lock
/// service's availability, and a lock that fails open under partition produces exactly the double
/// assignment it was bought to prevent.
/// </para>
/// </remarks>
public static class AcceptOffer
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/trips/{id}/accept/{driverId}", async (
                string id,
                string driverId,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(id, driverId), ct)));
    }

    public sealed record Request(string Id, string DriverId) : IRequest<Response>;

    public sealed record Response(bool Assigned);

    public sealed class RequestHandler(TripDbContext db, Clock clock) : IRequestHandler<Request, Response>
    {
        [Realizes("trips/dispatch", "first-acceptance-assigns")]
        [Realizes("trips/dispatch", "concurrent-acceptances-yield-one-assignment")]
        [Realizes("trips/dispatch", "late-acceptance-rejected")]
        [Realizes("trips/dispatch", "other-offers-withdrawn")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw new NotFoundException("no such trip", "trip:dispatch:accept:not_found");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var claimed = await db.Trips
                .Where(t => t.Id == id
                    && t.AssignedDriverId == null
                    && t.State == TripState.Requested)
                .ExecuteUpdateAsync(
                    t => t
                        .SetProperty(x => x.AssignedDriverId, request.DriverId)
                        .SetProperty(x => x.State, TripState.Assigned),
                    ct);

            if (claimed != 1)
            {
                await transaction.RollbackAsync(ct);
                throw new ConflictException("that offer has been taken", "trip:dispatch:accept:offer_taken");
            }

            db.TripTransitions.Add(new TripTransition
            {
                TripId = id,
                FromState = TripState.Requested,
                ToState = TripState.Assigned,
                Actor = request.DriverId,
                OccurredAt = clock.Now,
            });

            await db.Offers
                .Where(o => o.TripId == id && o.DriverId != request.DriverId && o.State == OfferState.Offered)
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.State, OfferState.Withdrawn), ct);

            await db.Offers
                .Where(o => o.TripId == id && o.DriverId == request.DriverId)
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.State, OfferState.Accepted), ct);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new Response(Assigned: true);
        }
    }
}
