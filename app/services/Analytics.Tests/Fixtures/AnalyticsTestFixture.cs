using Analytics.Database;
using Common.Messaging;
using Common.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Analytics.Tests.Fixtures;

public sealed class AnalyticsTestFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Start = new(2026, 8, 8, 4, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;

    public AnalyticsTestFixture()
    {
        Database = new DatabaseHarness<Program, AnalyticsDbContext>("analytics");
        RabbitMq = new RabbitMqHarness<Program>();
        HttpClient = new HttpClientHarness<Program>();
        Clock = new ClockHarness<Program>(Start);
        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(RabbitMq)
            .AddHarness(HttpClient)
            .AddHarness(Clock);
    }

    public DatabaseHarness<Program, AnalyticsDbContext> Database { get; }

    public RabbitMqHarness<Program> RabbitMq { get; }

    public HttpClientHarness<Program> HttpClient { get; }

    public ClockHarness<Program> Clock { get; }

    public async Task Reset(CancellationToken cancellation)
    {
        Clock.Reset();
        await Database.Clear(cancellation);
        await RabbitMq.Purge(
            cancellation,
            TripEventTopology.AnalyticsQueue,
            TripEventTopology.AnalyticsDeadLetterQueue,
            TripEventTopology.PaymentsQueue,
            TripEventTopology.PaymentsDeadLetterQueue);
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, Cancellation.Token(120));
        await RabbitMq.Start(_factory, Cancellation.Token(120));
        await HttpClient.Start(_factory, Cancellation.Token());
        await Clock.Start(_factory, Cancellation.Token());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Clock.Stop(Cancellation.Token());
        await HttpClient.Stop(Cancellation.Token());
        await RabbitMq.Stop(Cancellation.Token());
        await Database.Stop(Cancellation.Token());
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class TripActivityTestsCollection : ICollectionFixture<AnalyticsTestFixture>
{
    public const string Name = nameof(TripActivityTestsCollection);
}
