using System.Text.Json;

namespace Common.Messaging;

public sealed record PaymentCaptured(
    Guid EventId,
    long CaptureId,
    long TripId,
    long OriginalFareMinor,
    long ReferralCreditMinor,
    long CapturedAmountMinor,
    string Currency,
    long? ReferralCreditId,
    DateTimeOffset OccurredAt)
{
    public const int SchemaVersion = 1;

    public int Schema { get; init; } = SchemaVersion;
}

public static class PaymentCapturedCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize(PaymentCaptured message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static bool TryDeserialize(ReadOnlySpan<byte> body, out PaymentCaptured? message)
    {
        try
        {
            message = JsonSerializer.Deserialize<PaymentCaptured>(body, Options);
            return message is not null
                && message.Schema == PaymentCaptured.SchemaVersion
                && message.EventId != Guid.Empty
                && message.CaptureId > 0
                && message.TripId > 0
                && message.OriginalFareMinor >= 0
                && message.ReferralCreditMinor >= 0
                && message.CapturedAmountMinor >= 0
                && message.OriginalFareMinor - message.ReferralCreditMinor
                    == message.CapturedAmountMinor
                && ((message.ReferralCreditId is null && message.ReferralCreditMinor == 0)
                    || (message.ReferralCreditId > 0 && message.ReferralCreditMinor > 0))
                && !string.IsNullOrWhiteSpace(message.Currency);
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
    }
}
