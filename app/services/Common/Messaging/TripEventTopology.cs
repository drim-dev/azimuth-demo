using Azimuth.Annotations;
using RabbitMQ.Client;

namespace Common.Messaging;

public static class TripEventTopology
{
    public const string Exchange = "trip.lifecycle";
    public const string RoutingKey = "trip.state.changed";
    public const string PaymentsQueue = "trip.lifecycle.payments";
    public const string AnalyticsQueue = "trip.lifecycle.analytics";
    public const string PaymentsDeadLetterQueue = "trip.lifecycle.payments.dead";
    public const string AnalyticsDeadLetterQueue = "trip.lifecycle.analytics.dead";

    [Realizes("analytics/trip-activity", "latest-version-is-projected")]
    [Realizes("analytics/trip-activity", "redelivery-is-counted-once")]
    [Realizes("analytics/trip-activity", "older-delivery-is-inert")]
    [Realizes("analytics/trip-activity", "malformed-event-is-dead-lettered")]
    [Realizes("payments/capture", "capture-created-on-completion")]
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellation)
    {
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellation);

        await DeclareQueue(channel, PaymentsQueue, PaymentsDeadLetterQueue, cancellation);
        await DeclareQueue(channel, AnalyticsQueue, AnalyticsDeadLetterQueue, cancellation);
    }

    private static async Task DeclareQueue(
        IChannel channel,
        string queue,
        string deadLetterQueue,
        CancellationToken cancellation)
    {
        var deadLetterExchange = $"{queue}.dead";
        await channel.ExchangeDeclareAsync(
            deadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellation);
        await channel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellation);
        await channel.QueueBindAsync(
            deadLetterQueue,
            deadLetterExchange,
            string.Empty,
            cancellationToken: cancellation);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
        };
        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellation);
        await channel.QueueBindAsync(
            queue,
            Exchange,
            RoutingKey,
            cancellationToken: cancellation);
    }
}
