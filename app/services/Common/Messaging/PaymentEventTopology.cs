using RabbitMQ.Client;
using Azimuth.Annotations;

namespace Common.Messaging;

public static class PaymentEventTopology
{
    public const string Exchange = "payment.lifecycle";
    public const string RoutingKey = "payment.captured";
    public const string ReferralsQueue = "payment.lifecycle.referrals";
    public const string ReferralsDeadLetterQueue = "payment.lifecycle.referrals.dead";

    [ImplementsMechanism("payments/capture", "payment-event-topology")]
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellation)
    {
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellation);

        var deadLetterExchange = $"{ReferralsQueue}.dead";
        await channel.ExchangeDeclareAsync(
            deadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellation);
        await channel.QueueDeclareAsync(
            ReferralsDeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellation);
        await channel.QueueBindAsync(
            ReferralsDeadLetterQueue,
            deadLetterExchange,
            string.Empty,
            cancellationToken: cancellation);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
        };
        await channel.QueueDeclareAsync(
            ReferralsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellation);
        await channel.QueueBindAsync(
            ReferralsQueue,
            Exchange,
            RoutingKey,
            cancellationToken: cancellation);
    }
}
