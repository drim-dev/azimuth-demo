using Azimuth.Annotations;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Payments.Database;
using Payments.Domain;

namespace Payments.Features.Captures;

/// <summary>The intent the trip service writes in the same transaction as the completion.</summary>
public static class WriteCaptureIntent
{
    public sealed record Request(long TripId, long AmountMinor, string Currency) : IRequest;

    public sealed class RequestHandler(PaymentsDbContext db, Clock clock) : IRequestHandler<Request>
    {
        [Realizes("payments/capture", "capture-created-on-completion")]
        [Realizes("payments/capture", "no-capture-before-completion")]
        [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
        public async Task Handle(Request request, CancellationToken ct)
        {
            var alreadyWritten = await db.CaptureIntents
                .AsNoTracking()
                .AnyAsync(i => i.TripId == request.TripId, ct);

            if (alreadyWritten)
            {
                return;
            }

            db.CaptureIntents.Add(new CaptureIntent
            {
                TripId = request.TripId,
                AmountMinor = request.AmountMinor,
                Currency = request.Currency,
                WrittenAt = clock.Now,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
            {
                // A completion delivered twice writes one intent; the key settles which.
                db.ChangeTracker.Clear();
            }
        }
    }
}
