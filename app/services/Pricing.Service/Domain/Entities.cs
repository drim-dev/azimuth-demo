namespace Pricing.Service.Domain;

public sealed class MarketPressure
{
    public long Id { get; set; }
    public required string Market { get; set; }
    public int OpenRequests { get; set; }
    public int AvailableDrivers { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}

public sealed class IssuedQuote
{
    public long Id { get; set; }
    public required string Pickup { get; set; }
    public required string Dropoff { get; set; }
    public long TotalMinor { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public required string Token { get; set; }
}
