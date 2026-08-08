using Azimuth.Annotations;
using Common.Messaging;
using MediatR;

namespace Analytics.Features.TripActivity;

public sealed class TripLifecycleConsumer(
    IServiceScopeFactory scopes,
    RabbitMqAddress broker,
    ILogger<TripLifecycleConsumer> logger) : BackgroundService
{
    [Realizes("analytics/trip-activity", "malformed-event-is-dead-lettered")]
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(broker.Uri))
        {
            return Task.CompletedTask;
        }

        return TripEventReceiver.RunAsync(
            broker.Uri,
            TripEventTopology.AnalyticsQueue,
            Apply,
            logger,
            stoppingToken);
    }

    private async Task Apply(TripStateChanged message, CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ConsumeTripStateChanged.Request(message), cancellation);
    }
}
