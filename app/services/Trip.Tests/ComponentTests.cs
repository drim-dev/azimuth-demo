using Azimuth.Annotations;
using Testcontainers.PostgreSql;
using Pricing;
using Trip.Domain;
using Trip.Storage;
using Xunit;

namespace Trip.Tests;

/// <summary>
/// A real Postgres, because that is what <c>component</c> means (D15): real persistence and real
/// serialization, with external services substituted.
/// </summary>
/// <remarks>
/// Defined that way the rung is partly machine-checkable rather than purely self-declared — a
/// harness knows whether it started a database, so these claims cannot be quietly satisfied by an
/// in-memory fake. Every claim below is one whose truth is settled by a storage constraint, and
/// against a fake each of them would pass against an implementation that has no constraint at all.
/// </remarks>
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

[Collection("postgres")]
public sealed class AdmissionTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private QuoteStore Quotes => new(fixture.Database);

    private TripStore Trips => new(fixture.Database);

    private async Task<Guid> QuoteAsync(TimeSpan validFor) =>
        (await Quotes.IssueAsync("a", "b", Money.Of(1500, "EUR"), validFor, Now)).Id;

    private static string Rider() => $"rider-{Guid.NewGuid():N}";

    [Fact]
    [Covers("trip/request", "request-admitted-with-valid-quote", Scope.Component, Quantification.Invariant)]
    [Covers("trip/request", "trip-created-in-requested-state", Scope.Component, Quantification.Invariant)]
    public async Task A_valid_quote_admits_a_request_and_creates_one_trip()
    {
        var result = await Trips.AdmitAsync(Rider(), await QuoteAsync(TimeSpan.FromMinutes(2)), Now);

        Assert.True(result.Ok);
        var trip = await Trips.FindAsync(result.TripId);
        Assert.NotNull(trip);
        Assert.Equal(TripState.Requested, trip.State);
        Assert.Equal(1500, trip.Fare.MinorUnits);
    }

    [Fact]
    [Covers("trip/request", "request-rejected-with-expired-quote", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_is_refused_and_creates_nothing()
    {
        var quote = await QuoteAsync(TimeSpan.FromMinutes(-1));
        var result = await Trips.AdmitAsync(Rider(), quote, Now);

        Assert.False(result.Ok);
        Assert.Equal("expired-quote", result.Reason);
    }

    [Fact]
    [Covers("trip/request", "request-rejected-with-unknown-quote", Scope.Component, Quantification.Invariant, Oracle.Contract)]
    public async Task An_unrecognised_quote_is_refused()
    {
        var result = await Trips.AdmitAsync(Rider(), Guid.NewGuid(), Now);

        Assert.False(result.Ok);
        Assert.Equal("unknown-quote", result.Reason);
    }

    /// <summary>
    /// Quantified over concurrency: the sequential version of this passes against an implementation
    /// with no constraint at all, which is the whole reason the plan raises the scope.
    /// </summary>
    [Fact]
    [Covers("trip/request", "quote-consumed-once", Scope.Component, Quantification.Invariant)]
    public async Task A_quote_is_consumed_by_at_most_one_request_however_many_arrive_together()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var quote = await QuoteAsync(TimeSpan.FromMinutes(2));
            var riders = Enumerable.Range(0, 8).Select(_ => Rider()).ToArray();

            var results = await Task.WhenAll(
                riders.Select(rider => Trips.AdmitAsync(rider, quote, Now)));

            Assert.Equal(1, results.Count(r => r.Ok));
            Assert.All(
                results.Where(r => !r.Ok),
                r => Assert.Equal("quote-already-consumed", r.Reason));
        }
    }

    [Fact]
    [Covers("trip/request", "second-request-rejected-while-active", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_holds_at_most_one_active_trip_however_many_requests_arrive_together()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var rider = Rider();
            var quotes = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => QuoteAsync(TimeSpan.FromMinutes(2))));

            var results = await Task.WhenAll(
                quotes.Select(quote => Trips.AdmitAsync(rider, quote, Now)));

            Assert.Equal(1, results.Count(r => r.Ok));
        }
    }

    [Fact]
    [Covers("trip/request", "request-admitted-after-terminal", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_may_request_again_once_their_trip_is_terminal()
    {
        var rider = Rider();
        var first = await Trips.AdmitAsync(rider, await QuoteAsync(TimeSpan.FromMinutes(2)), Now);
        Assert.True(first.Ok);

        var blocked = await Trips.AdmitAsync(rider, await QuoteAsync(TimeSpan.FromMinutes(2)), Now);
        Assert.False(blocked.Ok);

        await Trips.ApplyAsync(first.TripId, TripEvent.Cancel, rider, Now);

        var again = await Trips.AdmitAsync(rider, await QuoteAsync(TimeSpan.FromMinutes(2)), Now);
        Assert.True(again.Ok);
    }

    [Fact]
    [Covers("pricing/quote", "quote-valid-before-expiry", Scope.Component, Quantification.Invariant)]
    [Covers("pricing/quote", "quote-invalid-after-expiry", Scope.Component, Quantification.Invariant)]
    public async Task A_quote_is_valid_until_its_expiry_and_not_after()
    {
        var valid = await Quotes.FindAsync(await QuoteAsync(TimeSpan.FromMinutes(2)));
        var expired = await Quotes.FindAsync(await QuoteAsync(TimeSpan.FromMinutes(-1)));

        Assert.NotNull(valid);
        Assert.NotNull(expired);
        Assert.True(valid.ExpiresAt > Now);
        Assert.True(expired.ExpiresAt <= Now);
        Assert.Equal(1500, expired.Total.MinorUnits);
    }

    [Fact]
    [Covers("pricing/quote", "expired-quote-is-never-revalidated", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_stays_expired_and_a_new_one_gets_a_new_identity()
    {
        var expired = await QuoteAsync(TimeSpan.FromMinutes(-1));
        var reissued = await QuoteAsync(TimeSpan.FromMinutes(2));

        Assert.NotEqual(expired, reissued);
        var original = await Quotes.FindAsync(expired);
        Assert.NotNull(original);
        Assert.True(original.ExpiresAt <= Now);
    }

    [Fact]
    [Covers("pricing/quote", "quote-returned", Scope.Component, Quantification.Invariant)]
    public async Task An_issued_quote_carries_a_total_a_currency_and_an_expiry()
    {
        var quote = await Quotes.IssueAsync("a", "b", Money.Of(1500, "EUR"), TimeSpan.FromMinutes(2), Now);

        Assert.NotEqual(Guid.Empty, quote.Id);
        Assert.Equal("EUR", quote.Total.Currency);
        Assert.Equal(Now + TimeSpan.FromMinutes(2), quote.ExpiresAt);
    }
}

[Collection("postgres")]
public sealed class DispatchTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private TripStore Trips => new(fixture.Database);

    private OfferStore Offers => new(fixture.Database);

    private async Task<Guid> TripAsync()
    {
        var quotes = new QuoteStore(fixture.Database);
        var quote = await quotes.IssueAsync("a", "b", Money.Of(1500, "EUR"), TimeSpan.FromMinutes(2), Now);
        var admitted = await Trips.AdmitAsync($"rider-{Guid.NewGuid():N}", quote.Id, Now);
        Assert.True(admitted.Ok);
        return admitted.TripId;
    }

    private async Task SeedDriversAsync(int available, int unavailable)
    {
        await using var connection = await fixture.Database.OpenAsync();
        for (var i = 0; i < available + unavailable; i++)
        {
            await using var command = new Npgsql.NpgsqlCommand(
                """
                INSERT INTO drivers (id, available, near, display, vehicle, position)
                VALUES (@id, @available, 'downtown', 'Sam', 'blue hatchback', '52.37,4.89')
                ON CONFLICT (id) DO NOTHING
                """,
                connection);
            command.Parameters.AddWithValue("id", $"driver-{i}");
            command.Parameters.AddWithValue("available", i < available);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// The claim quantifies over "any number of drivers accepting concurrently". A test that
    /// accepts twice sequentially satisfies the words and not the claim.
    /// </summary>
    [Fact]
    [Covers("trip/dispatch", "concurrent-acceptances-yield-one-assignment", Scope.Component, Quantification.Invariant)]
    [Covers("trip/dispatch", "first-acceptance-assigns", Scope.Component, Quantification.Invariant)]
    public async Task Exactly_one_driver_is_assigned_however_many_accept_together()
    {
        await SeedDriversAsync(available: 6, unavailable: 0);

        for (var trial = 0; trial < 5; trial++)
        {
            var trip = await TripAsync();
            await Offers.FanOutAsync(trip, "downtown", Now, TimeSpan.FromSeconds(30));

            var won = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(i => Offers.AcceptAsync(trip, $"driver-{i}", Now)));

            Assert.Equal(1, won.Count(w => w));
            var after = await Trips.FindAsync(trip);
            Assert.NotNull(after);
            Assert.Equal(TripState.Assigned, after.State);
            Assert.NotNull(after.AssignedDriverId);
        }
    }

    [Fact]
    [Covers("trip/dispatch", "late-acceptance-rejected", Scope.Component, Quantification.Invariant)]
    public async Task An_acceptance_after_assignment_changes_nothing()
    {
        await SeedDriversAsync(available: 2, unavailable: 0);
        var trip = await TripAsync();
        await Offers.FanOutAsync(trip, "downtown", Now, TimeSpan.FromSeconds(30));

        Assert.True(await Offers.AcceptAsync(trip, "driver-0", Now));
        var before = await Trips.FindAsync(trip);

        Assert.False(await Offers.AcceptAsync(trip, "driver-1", Now));
        var after = await Trips.FindAsync(trip);
        Assert.Equal(before!.AssignedDriverId, after!.AssignedDriverId);
    }

    [Fact]
    [Covers("trip/dispatch", "offer-sent-to-available-nearby-driver", Scope.Component, Quantification.Invariant)]
    [Covers("trip/dispatch", "unavailable-driver-not-offered", Scope.Component, Quantification.Invariant)]
    public async Task Only_available_nearby_drivers_are_offered()
    {
        await SeedDriversAsync(available: 3, unavailable: 2);
        var trip = await TripAsync();

        await Offers.FanOutAsync(trip, "downtown", Now, TimeSpan.FromSeconds(30));

        var offered = (await Offers.ForTripAsync(trip)).Select(o => o.DriverId).ToHashSet();
        Assert.Contains("driver-0", offered);
        Assert.DoesNotContain("driver-3", offered);
        Assert.DoesNotContain("driver-4", offered);
    }

    [Fact]
    [Covers("trip/dispatch", "no-available-drivers", Scope.Component, Quantification.Invariant)]
    public async Task No_available_drivers_means_no_offers()
    {
        var trip = await TripAsync();
        var offered = await Offers.FanOutAsync(trip, "nowhere", Now, TimeSpan.FromSeconds(30));

        Assert.Equal(0, offered);
        Assert.Empty(await Offers.ForTripAsync(trip));
    }

    [Fact]
    [Covers("trip/dispatch", "other-offers-withdrawn", Scope.Component, Quantification.Invariant)]
    public async Task Assignment_withdraws_every_other_offer()
    {
        await SeedDriversAsync(available: 4, unavailable: 0);
        var trip = await TripAsync();
        await Offers.FanOutAsync(trip, "downtown", Now, TimeSpan.FromSeconds(30));

        await Offers.AcceptAsync(trip, "driver-0", Now);

        var offers = await Offers.ForTripAsync(trip);
        Assert.Equal("accepted", offers.Single(o => o.DriverId == "driver-0").State);
        Assert.All(offers.Where(o => o.DriverId != "driver-0"), o => Assert.Equal("withdrawn", o.State));
    }

    [Fact]
    [Covers("trip/dispatch", "expired-offer-withdrawn", Scope.Component, Quantification.Example)]
    public async Task An_offer_past_its_expiry_is_withdrawn()
    {
        await SeedDriversAsync(available: 2, unavailable: 0);
        var trip = await TripAsync();
        await Offers.FanOutAsync(trip, "downtown", Now, TimeSpan.FromSeconds(30));

        await Offers.WithdrawExpiredAsync(Now + TimeSpan.FromMinutes(1));

        Assert.All(await Offers.ForTripAsync(trip), o => Assert.Equal("withdrawn", o.State));
    }
}

[Collection("postgres")]
public sealed class LifecycleTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private TripStore Trips => new(fixture.Database);

    private async Task<Guid> AssignedTripAsync()
    {
        var quotes = new QuoteStore(fixture.Database);
        var quote = await quotes.IssueAsync("a", "b", Money.Of(1500, "EUR"), TimeSpan.FromMinutes(2), Now);
        var admitted = await Trips.AdmitAsync($"rider-{Guid.NewGuid():N}", quote.Id, Now);
        await Trips.ApplyAsync(admitted.TripId, TripEvent.Assign, "driver-0", Now, "driver-0");
        return admitted.TripId;
    }

    /// <summary>
    /// The conditional write, not the machine. At unit scope this would verify that the handler
    /// compares a state it was handed, which is not the claim.
    /// </summary>
    [Fact]
    [Covers("trip/lifecycle", "replayed-transition-is-inert", Scope.Component, Quantification.Invariant)]
    public async Task A_replayed_transition_changes_nothing_however_many_times_it_arrives()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            var trip = await AssignedTripAsync();
            await Trips.ApplyAsync(trip, TripEvent.Start, "driver-0", Now);
            await Trips.ApplyAsync(trip, TripEvent.Complete, "driver-0", Now);

            var replays = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(_ => Trips.ApplyAsync(trip, TripEvent.Complete, "driver-0", Now)));

            Assert.All(replays, r => Assert.False(r.Ok));
            var after = await Trips.FindAsync(trip);
            Assert.Equal(TripState.Completed, after!.State);
        }
    }

    [Fact]
    [Covers("trip/lifecycle", "history-is-append-only", Scope.Component, Quantification.Invariant)]
    [Covers("trip/lifecycle", "transition-records-actor-and-instant", Scope.Component, Quantification.Invariant)]
    public async Task History_only_grows_and_records_who_caused_each_move()
    {
        var trip = await AssignedTripAsync();
        var afterAssign = await Trips.HistoryAsync(trip);

        await Trips.ApplyAsync(trip, TripEvent.Start, "driver-0", Now);
        await Trips.ApplyAsync(trip, TripEvent.Complete, "driver-0", Now);
        var afterComplete = await Trips.HistoryAsync(trip);

        Assert.Equal(afterAssign, afterComplete.Take(afterAssign.Count));
        Assert.Equal(afterAssign.Count + 2, afterComplete.Count);
        Assert.All(afterComplete, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Actor)));
        Assert.Equal(("in-progress", "completed", "driver-0"), afterComplete[^1]);
    }

    [Fact]
    [Covers("trip/lifecycle", "no-transition-out-of-terminal", Scope.Component, Quantification.Invariant)]
    public async Task A_terminal_trip_admits_no_event_against_a_real_store()
    {
        var trip = await AssignedTripAsync();
        await Trips.ApplyAsync(trip, TripEvent.Cancel, "rider", Now);

        foreach (var @event in TripStateMachine.Events)
        {
            var result = await Trips.ApplyAsync(trip, @event, "anyone", Now);
            Assert.False(result.Ok);
        }

        Assert.Equal(TripState.Cancelled, (await Trips.FindAsync(trip))!.State);
    }

    [Fact]
    [Covers("trip/lifecycle", "rider-cancels-before-start", Scope.Component, Quantification.Example)]
    [Covers("trip/lifecycle", "driver-cancels-after-assignment", Scope.Component, Quantification.Example)]
    [Covers("trip/lifecycle", "cancellation-after-completion-rejected", Scope.Component, Quantification.Example)]
    public async Task Cancellation_records_the_cancelling_party_and_is_refused_after_completion()
    {
        var byRider = await AssignedTripAsync();
        Assert.True((await Trips.ApplyAsync(byRider, TripEvent.Cancel, "rider", Now)).Ok);
        Assert.Equal("rider", (await Trips.HistoryAsync(byRider))[^1].Actor);

        var byDriver = await AssignedTripAsync();
        Assert.True((await Trips.ApplyAsync(byDriver, TripEvent.Cancel, "driver-0", Now)).Ok);
        Assert.Equal("driver-0", (await Trips.HistoryAsync(byDriver))[^1].Actor);

        var completed = await AssignedTripAsync();
        await Trips.ApplyAsync(completed, TripEvent.Start, "driver-0", Now);
        await Trips.ApplyAsync(completed, TripEvent.Complete, "driver-0", Now);
        Assert.False((await Trips.ApplyAsync(completed, TripEvent.Cancel, "rider", Now)).Ok);
    }
}
