using Azimuth.Annotations;
using Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trips.Database;
using Trips.Domain;
using Trips.Features.Events.Options;
using Common.Time;

namespace Trips.Features.Events;

public sealed class TripEventRelay(
    IServiceScopeFactory scopes,
    RabbitMqAddress broker,
    IOptions<TripEventRelayOptions> options,
    TripEventRelayState state,
    Clock clock,
    ILogger<TripEventRelay> logger) : BackgroundService
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
                logger.LogError(exception, "Trip event relay cycle failed");
            }

            await Task.Delay(options.Value.Interval, stoppingToken);
        }
    }

    [Realizes("analytics/trip-activity", "latest-version-is-projected")]
    [Realizes("payments/capture", "capture-created-on-completion")]
    public async Task<int> RelayPending(CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            broker.Uri,
            publisherConfirmations: true,
            cancellation);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
        var pending = await db.TripEvents
            .AsNoTracking()
            .Where(x => x.PublishedAt == null)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.EventId)
            .Take(100)
            .ToListAsync(cancellation);

        foreach (var item in pending)
        {
            await TripEventPublisher.PublishAsync(session.Channel, Message(item), cancellation);
            await db.TripEvents
                .Where(x => x.EventId == item.EventId && x.PublishedAt == null)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(e => e.PublishedAt, clock.Now),
                    cancellation);
        }

        return pending.Count;
    }

    private static TripStateChanged Message(TripEventOutbox item) => new(
        item.EventId,
        item.TripId,
        item.Version,
        TripStateMachine.Name(item.State),
        item.OccurredAt,
        item.QuoteToken,
        item.PaymentMethod);
}

public sealed class TripEventRelayState
{
    public DateTimeOffset? LastSuccess { get; set; }
}
