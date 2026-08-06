using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Common.Testing;

/// <summary>The way in: a test reaches a slice the way a client does, through its endpoint.</summary>
public sealed class HttpClientHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private WebApplicationFactory<TProgram>? _factory;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
    }

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellation)
    {
        _factory = factory;
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellation) => Task.CompletedTask;

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                $"HttpClient harness is not started. Call {nameof(Start)} first.");
        }

        return _factory.CreateClient();
    }
}
