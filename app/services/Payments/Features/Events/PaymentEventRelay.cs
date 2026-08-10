using Azimuth.Annotations;
using Common.Messaging;
using Common.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Database;
using Payments.Domain;
using Payments.Features.Events.Options;

namespace Payments.Features.Events;

public sealed class PaymentEventRelay(
    IServiceScopeFactory scopes,
    RabbitMqAddress broker,
    IOptions<PaymentEventRelayOptions> options,
    PaymentEventRelayState state,
    Clock clock,
    ILogger<PaymentEventRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(broker.Uri))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPending(stoppingToken);
                state.LastSuccess = clock.Now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Unmarked events remain retryable; stopping the worker would turn one broker fault
                // into silent permanent loss.
                logger.LogError(exception, "Payment event relay cycle failed");
            }

            await Task.Delay(options.Value.Interval, stoppingToken);
        }
    }

    [Realizes("payments/capture", "committed-capture-is-published")]
    [Realizes("payments/capture", "capture-publication-is-retryable")]
    public async Task<int> RelayPending(CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            broker.Uri,
            publisherConfirmations: true,
            cancellation);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var pending = await db.PaymentEvents
            .AsNoTracking()
            .Where(x => x.PublishedAt == null)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.EventId)
            .Take(100)
            .ToListAsync(cancellation);

        foreach (var item in pending)
        {
            await PaymentEventPublisher.PublishAsync(session.Channel, Message(item), cancellation);
            await db.PaymentEvents
                .Where(x => x.EventId == item.EventId && x.PublishedAt == null)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(e => e.PublishedAt, clock.Now),
                    cancellation);
        }

        return pending.Count;
    }

    private static PaymentCaptured Message(PaymentEventOutbox item) => new(
        item.EventId,
        item.CaptureId,
        item.TripId,
        item.OriginalFareMinor,
        item.ReferralCreditMinor,
        item.CapturedAmountMinor,
        item.Currency,
        item.ReferralCreditId,
        item.OccurredAt);
}

public sealed class PaymentEventRelayState
{
    public DateTimeOffset? LastSuccess { get; set; }
}
