using Common.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Common.Testing;

/// <summary>A real broker because acknowledgement, routing and dead-lettering are the claims.</summary>
public sealed class RabbitMqHarness<TProgram> : IHarness<TProgram> where TProgram : class
{
    private RabbitMqContainer? _rabbitMq;

    public string ConnectionString => _rabbitMq?.GetConnectionString()
        ?? throw new InvalidOperationException("RabbitMQ harness has not started");

    public void ConfigureWebHostBuilder(IWebHostBuilder builder) =>
        builder.UseSetting("RabbitMq:Uri", ConnectionString);

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellation)
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await _rabbitMq.StartAsync(cancellation);

        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: true,
            cancellation);
    }

    public async Task Publish(TripStateChanged message, CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: true,
            cancellation);
        await TripEventPublisher.PublishAsync(session.Channel, message, cancellation);
    }

    public async Task Publish(PaymentCaptured message, CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: true,
            cancellation);
        await PaymentEventPublisher.PublishAsync(session.Channel, message, cancellation);
    }

    public async Task PublishMalformed(ReadOnlyMemory<byte> body, CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: true,
            cancellation);
        await session.Channel.BasicPublishAsync(
            TripEventTopology.Exchange,
            TripEventTopology.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
            },
            body,
            cancellation);
    }

    public async Task PublishMalformedPayment(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: true,
            cancellation);
        await session.Channel.BasicPublishAsync(
            PaymentEventTopology.Exchange,
            PaymentEventTopology.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
            },
            body,
            cancellation);
    }

    public async Task<ReadOnlyMemory<byte>?> Get(string queue, CancellationToken cancellation)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: false,
            cancellation);
        var result = await session.Channel.BasicGetAsync(queue, autoAck: true, cancellation);
        return result?.Body;
    }

    public async Task Purge(CancellationToken cancellation, params string[] queues)
    {
        await using var session = await RabbitMqSession.OpenAsync(
            ConnectionString,
            publisherConfirmations: false,
            cancellation);
        foreach (var queue in queues)
        {
            await session.Channel.QueuePurgeAsync(queue, cancellation);
        }
    }

    public async Task Stop(CancellationToken cancellation)
    {
        if (_rabbitMq is not null)
        {
            await _rabbitMq.StopAsync(cancellation);
            await _rabbitMq.DisposeAsync();
        }
    }
}
