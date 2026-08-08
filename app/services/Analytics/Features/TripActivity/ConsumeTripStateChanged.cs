using Analytics.Database;
using Analytics.Domain;
using Azimuth.Annotations;
using Common.Messaging;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Features.TripActivity;

public static class ConsumeTripStateChanged
{
    public sealed record Request(TripStateChanged Event) : IRequest;

    public sealed class RequestHandler(AnalyticsDbContext db, Clock clock) : IRequestHandler<Request>
    {
        [Realizes("analytics/trip-activity", "latest-version-is-projected")]
        [Realizes("analytics/trip-activity", "redelivery-is-counted-once")]
        [Realizes("analytics/trip-activity", "older-delivery-is-inert")]
        public async Task Handle(Request request, CancellationToken cancellation)
        {
            var message = request.Event;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellation);

            if (await db.TripEventInbox.AsNoTracking()
                .AnyAsync(x => x.EventId == message.EventId, cancellation))
            {
                await transaction.CommitAsync(cancellation);
                return;
            }

            db.TripEventInbox.Add(new TripEventInbox
            {
                EventId = message.EventId,
                TripId = message.TripId,
                Version = message.Version,
                ReceivedAt = clock.Now,
            });

            var projection = await db.TripActivity.SingleOrDefaultAsync(
                x => x.TripId == message.TripId,
                cancellation);
            if (projection is null)
            {
                projection = new Domain.TripActivity
                {
                    TripId = message.TripId,
                    State = message.State,
                };
                db.TripActivity.Add(projection);
            }

            if (message.Version > projection.Version)
            {
                projection.Version = message.Version;
                projection.State = message.State;
                projection.OccurredAt = message.OccurredAt;
            }

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }
    }
}
