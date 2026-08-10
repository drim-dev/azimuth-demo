using Common.Messaging;
using MediatR;

namespace Trips.Features.Referrals;

public sealed class PaymentLifecycleConsumer(
    IServiceScopeFactory scopes,
    RabbitMqAddress broker,
    ILogger<PaymentLifecycleConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(broker.Uri))
        {
            return Task.CompletedTask;
        }

        return PaymentEventReceiver.RunAsync(
            broker.Uri,
            PaymentEventTopology.ReferralsQueue,
            Apply,
            logger,
            stoppingToken);
    }

    private async Task Apply(PaymentCaptured message, CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ConsumePaymentCaptured.Request(message), cancellation);
    }
}
