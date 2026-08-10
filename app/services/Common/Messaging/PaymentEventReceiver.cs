using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Messaging;

public static class PaymentEventReceiver
{
    public static async Task RunAsync(
        string uri,
        string queue,
        Func<PaymentCaptured, CancellationToken, Task> apply,
        ILogger logger,
        CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(uri, queue, apply, logger, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Payment lifecycle queue {Queue} is unavailable", queue);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation);
        }
    }

    private static async Task RunSessionAsync(
        string uri,
        string queue,
        Func<PaymentCaptured, CancellationToken, Task> apply,
        ILogger logger,
        CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            uri,
            publisherConfirmations: false,
            cancellation);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Channel.ChannelShutdownAsync += (_, _) =>
        {
            stopped.TrySetResult();
            return Task.CompletedTask;
        };
        await session.Channel.BasicQosAsync(0, 1, global: false, cancellation);

        var consumer = new AsyncEventingBasicConsumer(session.Channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            if (!PaymentCapturedCodec.TryDeserialize(delivery.Body.Span, out var message))
            {
                logger.LogError(
                    "Rejected malformed payment lifecycle event {MessageId}",
                    delivery.BasicProperties.MessageId);
                await session.Channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellation);
                return;
            }

            try
            {
                await apply(message!, cancellation);
                await session.Channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Deferred payment lifecycle event {EventId}", message!.EventId);
                await session.Channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellation);
            }
        };

        await session.Channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellation);
        await stopped.Task.WaitAsync(cancellation);
    }
}
