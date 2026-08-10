namespace Payments.Features.Events.Options;

public sealed class PaymentEventRelayOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);
}
