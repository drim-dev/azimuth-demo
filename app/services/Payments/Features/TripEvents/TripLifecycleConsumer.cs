using Common.Messaging;
using MediatR;

namespace Payments.Features.TripEvents;

public sealed class TripLifecycleConsumer(
    IServiceScopeFactory scopes,
    RabbitMqAddress broker,
    ILogger<TripLifecycleConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(broker.Uri))
        {
            return Task.CompletedTask;
        }

        return TripEventReceiver.RunAsync(
            broker.Uri,
            TripEventTopology.PaymentsQueue,
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
