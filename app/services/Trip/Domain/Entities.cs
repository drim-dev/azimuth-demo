using Pricing;

namespace Trips.Domain;

/// <summary>A price quoted for a journey, spendable by at most one request.</summary>
public sealed class Quote
{
    public long Id { get; set; }

    public required string Pickup { get; set; }

    public required string Dropoff { get; set; }

    public long TotalMinor { get; set; }

    public required string Currency { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set once, by the request that spends this quote. The partial unique index is what holds it.</summary>
    public long? ConsumedByTripId { get; set; }

    public Money Total => Money.Of(TotalMinor, Currency);

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;
}

public sealed class Trip
{
    public long Id { get; set; }

    public required string RiderId { get; set; }

    public string? AssignedDriverId { get; set; }

    public TripState State { get; set; }

    public long FareMinor { get; set; }

    public required string Currency { get; set; }

    public long QuoteId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<TripTransition> Transitions { get; set; } = [];

    public List<Offer> Offers { get; set; } = [];

    public Money Fare => Money.Of(FareMinor, Currency);
}

/// <summary>One move of a trip through the machine, written and never amended.</summary>
public sealed class TripTransition
{
    public long Id { get; set; }

    public long TripId { get; set; }

    public TripState FromState { get; set; }

    public TripState ToState { get; set; }

    public required string Actor { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public enum OfferState
{
    Offered,
    Accepted,
    Withdrawn,
}

public sealed class Offer
{
    public long TripId { get; set; }

    public required string DriverId { get; set; }

    public OfferState State { get; set; }

    public DateTimeOffset OfferedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class Driver
{
    public required string Id { get; set; }

    public bool Available { get; set; }

    public required string Near { get; set; }

    public required string Display { get; set; }

    public required string Vehicle { get; set; }

    public string? Position { get; set; }
}

public static class OfferStateNames
{
    public static string Of(OfferState state) => state switch
    {
        OfferState.Offered => "offered",
        OfferState.Accepted => "accepted",
        OfferState.Withdrawn => "withdrawn",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    public static OfferState Parse(string name) => name switch
    {
        "offered" => OfferState.Offered,
        "accepted" => OfferState.Accepted,
        "withdrawn" => OfferState.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown offer state"),
    };
}
