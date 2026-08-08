namespace Payments.Features.Captures;

public sealed class CaptureSettlementState
{
    private long _lastSuccessUtcTicks;

    public DateTimeOffset? LastSuccess
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessUtcTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void RecordSuccess(DateTimeOffset instant) =>
        Interlocked.Exchange(ref _lastSuccessUtcTicks, instant.UtcTicks);
}
