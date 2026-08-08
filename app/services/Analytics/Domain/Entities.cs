namespace Analytics.Domain;

public sealed class TripActivity
{
    public long TripId { get; set; }

    public long Version { get; set; }

    public required string State { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class TripEventInbox
{
    public Guid EventId { get; set; }

    public long TripId { get; set; }

    public long Version { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
