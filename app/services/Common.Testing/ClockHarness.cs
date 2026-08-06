using Common.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Testing;

/// <summary>
/// Time, held still and moved on purpose.
/// </summary>
/// <remarks>
/// A harness like any other: expiry is an external dependency in the sense that matters here — the
/// service reads it, the test must control it, and a test that instead waited for a real clock
/// would be slow where it was reliable and flaky where it was fast. Claims about what a quote or an
/// offer does after its expiry are settled by moving this, not by sleeping.
/// </remarks>
public sealed class ClockHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private readonly DateTimeOffset _start;
    private DateTimeOffset _now;

    public ClockHarness(DateTimeOffset start)
    {
        _start = start;
        _now = start;
    }

    public DateTimeOffset Now => _now;

    public void Set(DateTimeOffset instant) => _now = instant;

    public void Advance(TimeSpan by) => _now += by;

    public void Reset() => _now = _start;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Clock>();
            services.AddSingleton(new Clock(() => _now));
        });

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellation) =>
        Task.CompletedTask;

    public Task Stop(CancellationToken cancellation) => Task.CompletedTask;
}
