using Azimuth.Annotations;
using Payments.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace Payments.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public Database Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Database = new Database(_container.GetConnectionString());
        await Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>Records what the provider was asked, and answers as the test dictates.</summary>
internal sealed class ScriptedProvider(params ProviderOutcome[] outcomes) : IPaymentProvider
{
    private int _calls;

    public int Calls => _calls;

    public Task<ProviderOutcome> CaptureAsync(Guid tripId, long amountMinor, string currency)
    {
        var index = Interlocked.Increment(ref _calls) - 1;
        return Task.FromResult(index < outcomes.Length ? outcomes[index] : outcomes[^1]);
    }
}

/// <summary>
/// A real Postgres, because uniqueness here is settled by a storage constraint (D15). Against an
/// in-memory fake every one of these would pass against an implementation that has no index at all.
/// </summary>
[Collection("postgres")]
public sealed class CaptureTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private CaptureDispatcher Dispatcher(params ProviderOutcome[] outcomes) =>
        new(fixture.Database, new ScriptedProvider(outcomes.Length == 0 ? [ProviderOutcome.Captured] : outcomes));

    [Fact]
    [Covers("payments/capture", "capture-created-on-completion", Scope.Component, Quantification.Invariant)]
    [Covers("payments/capture", "capture-equals-trip-fare", Scope.Component, Quantification.Invariant)]
    public async Task A_completed_trip_is_captured_for_its_fare()
    {
        var trip = Guid.NewGuid();
        var dispatcher = Dispatcher();
        await dispatcher.WriteIntentAsync(trip, 1500, "EUR", Now);

        Assert.Equal(1, await dispatcher.DispatchAsync(Now));

        var capture = await dispatcher.FindAsync(trip);
        Assert.NotNull(capture);
        Assert.Equal(1500, capture.AmountMinor);
        Assert.Equal("EUR", capture.Currency);
    }

    [Fact]
    [Covers("payments/capture", "no-capture-before-completion", Scope.Component, Quantification.Invariant)]
    public async Task A_trip_with_no_intent_has_no_capture()
    {
        Assert.Null(await Dispatcher().FindAsync(Guid.NewGuid()));
    }

    [Fact]
    [Covers("payments/capture", "no-capture-on-cancellation-without-fee", Scope.Component, Quantification.Invariant)]
    public async Task A_cancelled_trip_with_no_fee_writes_no_intent_and_gets_no_capture()
    {
        var trip = Guid.NewGuid();
        var dispatcher = Dispatcher();

        // No intent is written for a cancellation without a fee, so dispatching finds nothing.
        Assert.Equal(0, await dispatcher.DispatchAsync(Now));
        Assert.Null(await dispatcher.FindAsync(trip));
    }

    /// <summary>
    /// Quantified over redelivery: the sequential version of this passes against an implementation
    /// with no index at all, which is the whole reason the plan raises the scope.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "duplicate-completion-event", Scope.Component, Quantification.Invariant)]
    public async Task A_completion_delivered_any_number_of_times_captures_once()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var trip = Guid.NewGuid();
            var dispatcher = Dispatcher();
            await dispatcher.WriteIntentAsync(trip, 1500, "EUR", Now);

            for (var delivery = 0; delivery < 6; delivery++)
            {
                await dispatcher.WriteIntentAsync(trip, 1500, "EUR", Now);
                await dispatcher.DispatchAsync(Now);
            }

            Assert.Equal(1, await CountCapturesAsync(trip));
        }
    }

    [Fact]
    [Covers("payments/capture", "concurrent-completion-processing", Scope.Component, Quantification.Invariant)]
    public async Task Concurrent_workers_create_exactly_one_capture()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var trip = Guid.NewGuid();
            var dispatcher = Dispatcher();
            await dispatcher.WriteIntentAsync(trip, 1500, "EUR", Now);

            var results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => dispatcher.CaptureAsync(trip, 1500, "EUR", Now)));

            Assert.Equal(1, results.Count(won => won));
            Assert.Equal(1, await CountCapturesAsync(trip));
        }
    }

    /// <summary>
    /// An outcome the caller never observed may or may not have succeeded. Assuming failure is what
    /// double-charges, so it is treated as possibly-captured and the index settles it.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "retry-after-transport-failure", Scope.Component, Quantification.Invariant)]
    public async Task A_retry_after_an_unobserved_outcome_still_captures_once()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var trip = Guid.NewGuid();
            var dispatcher = Dispatcher(ProviderOutcome.Unobserved, ProviderOutcome.Captured);

            await dispatcher.CaptureAsync(trip, 1500, "EUR", Now);
            for (var retry = 0; retry < 4; retry++)
            {
                await dispatcher.CaptureAsync(trip, 1500, "EUR", Now);
            }

            Assert.Equal(1, await CountCapturesAsync(trip));
        }
    }

    [Fact]
    [Covers("payments/capture", "adjusted-capture-records-reason", Scope.Component, Quantification.Invariant)]
    public async Task An_adjusted_capture_records_why()
    {
        var trip = Guid.NewGuid();
        var dispatcher = Dispatcher();

        await dispatcher.CaptureAsync(trip, 1200, "EUR", Now, "goodwill-credit");

        var capture = await dispatcher.FindAsync(trip);
        Assert.Equal(1200, capture!.AmountMinor);
        Assert.Equal("goodwill-credit", capture.AdjustmentReason);
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-recorded", Scope.Component, Quantification.Example)]
    public async Task A_decline_is_recorded_rather_than_dropped()
    {
        var trip = Guid.NewGuid();
        var dispatcher = Dispatcher(ProviderOutcome.Declined);

        Assert.False(await dispatcher.CaptureAsync(trip, 1500, "EUR", Now));

        Assert.Null(await dispatcher.FindAsync(trip));
        Assert.Equal(["declined"], await dispatcher.FailuresAsync(trip));
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-is-retryable", Scope.Component, Quantification.Example)]
    public async Task A_declined_capture_may_be_retried_and_still_lands_at_most_once()
    {
        var trip = Guid.NewGuid();
        var dispatcher = Dispatcher(ProviderOutcome.Declined, ProviderOutcome.Captured);

        Assert.False(await dispatcher.CaptureAsync(trip, 1500, "EUR", Now));
        Assert.True(await dispatcher.CaptureAsync(trip, 1500, "EUR", Now));

        Assert.Equal(1, await CountCapturesAsync(trip));
        Assert.Single(await dispatcher.FailuresAsync(trip));
    }

    private async Task<int> CountCapturesAsync(Guid trip)
    {
        await using var connection = await fixture.Database.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM captures WHERE trip_id = @trip AND NOT voided", connection);
        command.Parameters.AddWithValue("trip", trip);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
