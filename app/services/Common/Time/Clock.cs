namespace Common.Time;

/// <summary>Injected so tests can settle time without waiting for it.</summary>
public sealed class Clock(Func<DateTimeOffset> now)
{
    public static Clock System => new(() => DateTimeOffset.UtcNow);

    public static Clock Fixed(DateTimeOffset instant) => new(() => instant);

    public DateTimeOffset Now => now();
}
