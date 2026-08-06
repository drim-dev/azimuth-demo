using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Common.Testing;

/// <summary>
/// An external dependency a component test controls: it starts the thing, points the service at
/// it, seeds it, answers questions about it, and stops it.
/// </summary>
/// <remarks>
/// The point of the abstraction is that a test names what it needs rather than how the service was
/// wired to get it. A slice that changes its internals keeps its tests; a slice that changes what
/// it depends on changes a harness, once, for every test that uses it.
/// </remarks>
public interface IHarness<TProgram>
    where TProgram : class
{
    void ConfigureWebHostBuilder(IWebHostBuilder builder);

    Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellation);

    Task Stop(CancellationToken cancellation);
}

public static class HarnessExtensions
{
    public static WebApplicationFactory<TProgram> AddHarness<TProgram>(
        this WebApplicationFactory<TProgram> factory,
        IHarness<TProgram> harness)
        where TProgram : class =>
        factory.WithWebHostBuilder(harness.ConfigureWebHostBuilder);
}

public static class Cancellation
{
    public static CancellationToken Token(int seconds = 30) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;
}
