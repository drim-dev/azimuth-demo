using Common.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Pricing.Service.Database;
using Xunit;

namespace Pricing.Tests.Fixtures;

public sealed class PricingTestFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Start = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;

    public PricingTestFixture()
    {
        Database = new DatabaseHarness<Program, PricingDbContext>("pricing");
        HttpClient = new HttpClientHarness<Program>();
        Clock = new ClockHarness<Program>(Start);
        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient)
            .AddHarness(Clock);
    }

    public DatabaseHarness<Program, PricingDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }
    public ClockHarness<Program> Clock { get; }

    public async Task Reset(CancellationToken cancellation)
    {
        Clock.Reset();
        await Database.Clear(cancellation);
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, Cancellation.Token(120));
        await HttpClient.Start(_factory, Cancellation.Token());
        await Clock.Start(_factory, Cancellation.Token());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Clock.Stop(Cancellation.Token());
        await HttpClient.Stop(Cancellation.Token());
        await Database.Stop(Cancellation.Token());
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PricingTestsCollection : ICollectionFixture<PricingTestFixture>
{
    public const string Name = nameof(PricingTestsCollection);
}
