using Common.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Trips.Database;
using Xunit;

namespace Trips.Tests.Fixtures;

/// <summary>
/// The trip service as a client meets it: real slices, real Postgres, time under the test's hand.
/// </summary>
public sealed class TripTestFixture : IAsyncLifetime
{
    /// <summary>The instant every test starts from, so an expiry is a decision rather than a wait.</summary>
    public static readonly DateTimeOffset Start = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;

    public TripTestFixture()
    {
        Database = new DatabaseHarness<Program, TripDbContext>("trip");
        HttpClient = new HttpClientHarness<Program>();
        Clock = new ClockHarness<Program>(Start);

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient)
            .AddHarness(Clock);
    }

    public DatabaseHarness<Program, TripDbContext> Database { get; }

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

        // Forces the host up, which runs the service's own migration against the container.
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
public sealed class QuotesTestsCollection : ICollectionFixture<TripTestFixture>
{
    public const string Name = nameof(QuotesTestsCollection);
}

[CollectionDefinition(Name)]
public sealed class AdmissionTestsCollection : ICollectionFixture<TripTestFixture>
{
    public const string Name = nameof(AdmissionTestsCollection);
}

[CollectionDefinition(Name)]
public sealed class DispatchTestsCollection : ICollectionFixture<TripTestFixture>
{
    public const string Name = nameof(DispatchTestsCollection);
}

[CollectionDefinition(Name)]
public sealed class LifecycleTestsCollection : ICollectionFixture<TripTestFixture>
{
    public const string Name = nameof(LifecycleTestsCollection);
}
