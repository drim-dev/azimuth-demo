using Azimuth.Annotations;
using Common.Identity;
using Common.Messaging;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Referrals;

public static class ConsumePaymentCaptured
{
    private const long RewardMinor = 500;

    public sealed record Request(PaymentCaptured Event) : IRequest;

    public sealed class RequestHandler(TripDbContext db, IdFactory ids, Clock clock)
        : IRequestHandler<Request>
    {
        [Realizes("referrals/rewards", "no-reward-before-capture")]
        [Realizes("referrals/rewards", "first-capture-awards-pair")]
        [Realizes("referrals/rewards", "capture-redelivery-does-not-duplicate-reward")]
        [Realizes("referrals/rewards", "capture-redelivery-does-not-redeem-twice")]
        public async Task Handle(Request request, CancellationToken cancellation)
        {
            var message = request.Event;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellation);

            // The trip lock serializes distinct event ids for one logical capture; the inbox only
            // settles exact broker redelivery and cannot do that on its own.
            var trip = await db.Trips
                .FromSql($"SELECT * FROM trips WHERE id = {message.TripId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellation)
                ?? throw new InvalidOperationException(
                    $"payment capture {message.CaptureId} names unknown trip {message.TripId}");

            if (await db.PaymentEventInbox.AsNoTracking()
                .AnyAsync(x => x.EventId == message.EventId, cancellation))
            {
                await transaction.CommitAsync(cancellation);
                return;
            }

            if (message.OriginalFareMinor != trip.FareMinor
                || !string.Equals(message.Currency, trip.Currency, StringComparison.Ordinal)
                || message.ReferralCreditId != trip.ReferralCreditId)
            {
                throw new InvalidOperationException(
                    $"payment capture {message.CaptureId} disagrees with trip {message.TripId}");
            }

            if (message.ReferralCreditId is { } creditId)
            {
                var credit = await db.ReferralCredits.SingleOrDefaultAsync(
                    x => x.Id == creditId,
                    cancellation) ?? throw new InvalidOperationException(
                        $"payment capture {message.CaptureId} names unknown referral credit {creditId}");

                if (credit.AmountMinor != message.ReferralCreditMinor
                    || credit.Currency != message.Currency
                    || credit.ReservedTripId != trip.Id)
                {
                    throw new InvalidOperationException(
                        $"payment capture {message.CaptureId} disagrees with referral credit {creditId}");
                }

                if (credit.State == ReferralCreditState.Reserved)
                {
                    credit.State = ReferralCreditState.Used;
                    credit.UsedCaptureId = message.CaptureId;
                }
                else if (credit.State != ReferralCreditState.Used
                    || credit.UsedCaptureId != message.CaptureId)
                {
                    throw new InvalidOperationException(
                        $"referral credit {creditId} was not reserved for this capture");
                }
            }

            var attribution = await db.ReferralAttributions.SingleOrDefaultAsync(
                x => x.ReferredRiderId == trip.RiderId,
                cancellation);
            if (attribution is { QualificationCaptureId: null })
            {
                attribution.QualificationCaptureId = message.CaptureId;
                db.ReferralCredits.AddRange(
                    Reward(ids.Create(), attribution.Id, attribution.ReferrerRiderId, message.Currency),
                    Reward(ids.Create(), attribution.Id, attribution.ReferredRiderId, message.Currency));
            }

            db.PaymentEventInbox.Add(new PaymentEventInbox
            {
                EventId = message.EventId,
                CaptureId = message.CaptureId,
                TripId = message.TripId,
                ReceivedAt = clock.Now,
            });

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }

        private ReferralCredit Reward(
            long id,
            long attributionId,
            string riderId,
            string currency) => new()
        {
            Id = id,
            AttributionId = attributionId,
            BeneficiaryRiderId = riderId,
            AmountMinor = RewardMinor,
            Currency = currency,
            State = ReferralCreditState.Available,
            CreatedAt = clock.Now,
        };
    }
}
