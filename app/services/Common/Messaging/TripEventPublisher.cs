using RabbitMQ.Client;

namespace Common.Messaging;

public static class TripEventPublisher
{
    public static Task PublishAsync(
        IChannel channel,
        TripStateChanged message,
        CancellationToken cancellation) =>
        channel.BasicPublishAsync(
            TripEventTopology.Exchange,
            TripEventTopology.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.EventId.ToString("D"),
                Type = nameof(TripStateChanged),
            },
            body: TripStateChangedCodec.Serialize(message),
            cancellationToken: cancellation).AsTask();
}
