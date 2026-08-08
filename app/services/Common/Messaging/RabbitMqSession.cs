using RabbitMQ.Client;

namespace Common.Messaging;

public sealed class RabbitMqSession : IAsyncDisposable
{
    private readonly IConnection _connection;

    private RabbitMqSession(IConnection connection, IChannel channel)
    {
        _connection = connection;
        Channel = channel;
    }

    public IChannel Channel { get; }

    public static async Task<RabbitMqSession> OpenAsync(
        string uri,
        bool publisherConfirmations,
        CancellationToken cancellation)
    {
        var factory = new ConnectionFactory { Uri = new Uri(uri) };
        var connection = await factory.CreateConnectionAsync(cancellation);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: publisherConfirmations,
            publisherConfirmationTrackingEnabled: publisherConfirmations);
        var channel = await connection.CreateChannelAsync(options, cancellation);
        await TripEventTopology.DeclareAsync(channel, cancellation);
        return new RabbitMqSession(connection, channel);
    }

    public async ValueTask DisposeAsync()
    {
        await Channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
