namespace Payments.Domain;

/// <summary>
/// The outbox row the trip service writes in the same transaction as the completion.
/// </summary>
/// <remarks>
/// Keyed by trip, so a completion delivered twice writes one intent. The dispatcher is its only
/// reader.
/// </remarks>
public sealed class CaptureIntent
{
    public long TripId { get; set; }

    public required string QuoteToken { get; set; }

    public DateTimeOffset WrittenAt { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed class Capture
{
    public long Id { get; set; }

    public long TripId { get; set; }

    public long AmountMinor { get; set; }

    public required string Currency { get; set; }

    public string? AdjustmentReason { get; set; }

    /// <summary>A voided capture stays as a row so that a disputed trip's history is legible.</summary>
    public bool Voided { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
}

public sealed class CaptureFailure
{
    public long Id { get; set; }

    public long TripId { get; set; }

    public required string Reason { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public enum ProviderOutcome
{
    Captured,
    Declined,

    /// <summary>The caller never observed the outcome, so it may or may not have succeeded.</summary>
    Unobserved,
}

public interface IPaymentProvider
{
    Task<ProviderOutcome> CaptureAsync(long tripId, long amountMinor, string currency);
}
