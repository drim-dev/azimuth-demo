namespace Payments.Features.Captures.Options;

public sealed class CaptureSettlementOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan OverdueAfter { get; set; } = TimeSpan.FromMinutes(2);
}
