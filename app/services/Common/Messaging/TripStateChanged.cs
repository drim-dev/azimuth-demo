using System.Text.Json;

namespace Common.Messaging;

public sealed record RabbitMqAddress(string Uri);

public sealed record TripStateChanged(
    Guid EventId,
    long TripId,
    long Version,
    string State,
    DateTimeOffset OccurredAt,
    string QuoteToken,
    string PaymentMethod,
    string? ReferralCreditAuthority = null)
{
    public const int SchemaVersion = 1;

    public int Schema { get; init; } = SchemaVersion;
}

public static class TripStateChangedCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> States =
    [
        "requested",
        "assigned",
        "in-progress",
        "completed",
        "cancelled",
    ];

    public static byte[] Serialize(TripStateChanged message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static bool TryDeserialize(ReadOnlySpan<byte> body, out TripStateChanged? message)
    {
        try
        {
            message = JsonSerializer.Deserialize<TripStateChanged>(body, Options);
            return message is not null
                && message.Schema == TripStateChanged.SchemaVersion
                && message.EventId != Guid.Empty
                && message.TripId > 0
                && message.Version > 0
                && States.Contains(message.State)
                && !string.IsNullOrWhiteSpace(message.QuoteToken)
                && !string.IsNullOrWhiteSpace(message.PaymentMethod)
                && (message.ReferralCreditAuthority is null
                    || !string.IsNullOrWhiteSpace(message.ReferralCreditAuthority));
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
    }
}
