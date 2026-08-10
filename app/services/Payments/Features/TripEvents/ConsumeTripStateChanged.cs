using Azimuth.Annotations;
using Common.Messaging;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;
using Payments.Domain;

namespace Payments.Features.TripEvents;

public static class ConsumeTripStateChanged
{
    public sealed record Request(TripStateChanged Event) : IRequest;

    public sealed class RequestHandler(PaymentsDbContext db, Clock clock) : IRequestHandler<Request>
    {
        [Realizes("payments/capture", "capture-created-on-completion")]
        [Realizes("payments/capture", "no-capture-before-completion")]
        [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
        [Realizes("payments/capture", "duplicate-completion-event")]
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
                State = message.State,
                ReceivedAt = clock.Now,
            });

            var cursor = await db.TripEventCursors.SingleOrDefaultAsync(
                x => x.TripId == message.TripId,
                cancellation);
            if (cursor is null)
            {
                cursor = new TripEventCursor { TripId = message.TripId };
                db.TripEventCursors.Add(cursor);
            }

            if (message.Version > cursor.Version)
            {
                cursor.Version = message.Version;
                if (message.State == "completed"
                    && !await db.CaptureIntents.AsNoTracking()
                        .AnyAsync(x => x.TripId == message.TripId, cancellation))
                {
                    db.CaptureIntents.Add(new CaptureIntent
                    {
                        TripId = message.TripId,
                        QuoteToken = message.QuoteToken,
                        PaymentMethod = message.PaymentMethod,
                        ReferralCreditAuthority = message.ReferralCreditAuthority,
                        WrittenAt = message.OccurredAt,
                    });
                }
            }

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }
    }
}
