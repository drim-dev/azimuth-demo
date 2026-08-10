using Common.Testing;
using Common.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Payments.Database;
using Xunit;

namespace Payments.Tests.Fixtures;

/// <summary>
/// A real Postgres, because uniqueness here is settled by a storage constraint (D15). Against an
/// in-memory fake every capture claim would pass against an implementation that has no index at all.
/// </summary>
public sealed class PaymentsTestFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Start = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsTestFixture()
    {
        Database = new DatabaseHarness<Program, PaymentsDbContext>("payments");
        HttpClient = new HttpClientHarness<Program>();
        Clock = new ClockHarness<Program>(Start);
        Provider = new PaymentProviderHarness();
        RabbitMq = new RabbitMqHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("CaptureSettlement:Enabled", "false");
                builder.UseSetting("PaymentEventRelay:Enabled", "false");
            })
            .AddHarness(Database)
            .AddHarness(RabbitMq)
            .AddHarness(HttpClient)
            .AddHarness(Clock)
            .AddHarness(Provider);
    }

    public DatabaseHarness<Program, PaymentsDbContext> Database { get; }

    public HttpClientHarness<Program> HttpClient { get; }

    public ClockHarness<Program> Clock { get; }

    public PaymentProviderHarness Provider { get; }

    public RabbitMqHarness<Program> RabbitMq { get; }

    public T Service<T>() where T : notnull => _factory.Services.GetRequiredService<T>();

    public async Task Reset(CancellationToken cancellation)
    {
        Clock.Reset();
        Provider.Reset();
        await Database.Clear(cancellation);
        await RabbitMq.Purge(
            cancellation,
            TripEventTopology.PaymentsQueue,
            TripEventTopology.PaymentsDeadLetterQueue,
            TripEventTopology.AnalyticsQueue,
            TripEventTopology.AnalyticsDeadLetterQueue,
            PaymentEventTopology.ReferralsQueue,
            PaymentEventTopology.ReferralsDeadLetterQueue);
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, Cancellation.Token(120));
        await RabbitMq.Start(_factory, Cancellation.Token(120));
        await HttpClient.Start(_factory, Cancellation.Token());
        await Clock.Start(_factory, Cancellation.Token());
        await Provider.Start(_factory, Cancellation.Token());

        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Provider.Stop(Cancellation.Token());
        await Clock.Stop(Cancellation.Token());
        await HttpClient.Stop(Cancellation.Token());
        await RabbitMq.Stop(Cancellation.Token());
        await Database.Stop(Cancellation.Token());
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class CaptureTestsCollection : ICollectionFixture<PaymentsTestFixture>
{
    public const string Name = nameof(CaptureTestsCollection);
}
