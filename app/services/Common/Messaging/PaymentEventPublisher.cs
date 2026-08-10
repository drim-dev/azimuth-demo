using RabbitMQ.Client;

namespace Common.Messaging;

public static class PaymentEventPublisher
{
    public static Task PublishAsync(
        IChannel channel,
        PaymentCaptured message,
        CancellationToken cancellation) =>
        channel.BasicPublishAsync(
            PaymentEventTopology.Exchange,
            PaymentEventTopology.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.EventId.ToString("D"),
                Type = nameof(PaymentCaptured),
            },
            body: PaymentCapturedCodec.Serialize(message),
            cancellationToken: cancellation).AsTask();
}
