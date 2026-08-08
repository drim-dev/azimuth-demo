namespace Trips.Features.Events.Options;

public sealed class TripEventRelayOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);
}
