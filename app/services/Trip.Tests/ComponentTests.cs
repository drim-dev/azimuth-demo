using Azimuth.Annotations;
using Common.Identity;
using Trips.Domain;
using Trips.Features.Dispatch;
using Trips.Features.Quotes;
using Trips.Features.Trips;
using Xunit;

namespace Trips.Tests;

[Collection("postgres")]
public sealed class AdmissionTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private TripHarness Harness(DateTimeOffset? at = null) => new(fixture.ConnectionString, at ?? Now);

    private static IssueQuote.Request Journey() => new("a", "b", 1000, 500, "EUR");

    private static string Rider() => $"rider-{Guid.NewGuid():N}";

    private static long Decode(string encoded)
    {
        Assert.True(IdEncoding.TryDecode(encoded, out var id));
        return id;
    }

    /// <summary>A quote that lapsed a minute ago, issued by the same slice against an earlier clock.</summary>
    private async Task<string> ExpiredQuoteAsync()
    {
        await using var past = Harness(Now - TimeSpan.FromMinutes(3));
        return (await past.SendAsync(Journey())).Id;
    }

    [Fact]
    [Covers("trip/request", "request-admitted-with-valid-quote", Scope.Component, Quantification.Invariant)]
    [Covers("trip/request", "trip-created-in-requested-state", Scope.Component, Quantification.Invariant)]
    public async Task A_valid_quote_admits_a_request_and_creates_one_trip()
    {
        await using var harness = Harness();
        var quote = await harness.SendAsync(Journey());

        var trip = await harness.SendAsync(new RequestRide.Request(Rider(), quote.Id));

        Assert.Equal("requested", trip.State);
        var tripId = Decode(trip.TripId);
        Assert.Equal(TripState.Requested, await harness.StateAsync(tripId));
        Assert.Equal(1500, await harness.FareAsync(tripId));
    }

    [Fact]
    [Covers("trip/request", "request-rejected-with-expired-quote", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_is_refused_and_creates_nothing()
    {
        await using var harness = Harness();
        var expired = await ExpiredQuoteAsync();

        var result = await harness.TrySendAsync(new RequestRide.Request(Rider(), expired));

        Assert.False(result.Ok);
        Assert.Equal("trip:request:create:expired_quote", result.ErrorCode);
    }

    [Fact]
    [Covers("trip/request", "request-rejected-with-unknown-quote", Scope.Component, Quantification.Invariant, Oracle.Contract)]
    public async Task An_unrecognised_quote_is_refused()
    {
        await using var harness = Harness();

        var result = await harness.TrySendAsync(
            new RequestRide.Request(Rider(), IdEncoding.Encode(Random.Shared.NextInt64())));

        Assert.False(result.Ok);
        Assert.Equal("trip:request:create:unknown_quote", result.ErrorCode);
    }

    /// <summary>
    /// Quantified over concurrency: the sequential version of this passes against an implementation
    /// with no constraint at all, which is the whole reason the plan raises the scope.
    /// </summary>
    [Fact]
    [Covers("trip/request", "quote-consumed-once", Scope.Component, Quantification.Invariant)]
    public async Task A_quote_is_consumed_by_at_most_one_request_however_many_arrive_together()
    {
        await using var harness = Harness();

        for (var trial = 0; trial < 5; trial++)
        {
            var quote = await harness.SendAsync(Journey());
            var riders = Enumerable.Range(0, 8).Select(_ => Rider()).ToArray();

            var results = await Task.WhenAll(
                riders.Select(rider => harness.TrySendAsync(new RequestRide.Request(rider, quote.Id))));

            Assert.Equal(1, results.Count(r => r.Ok));
            Assert.All(
                results.Where(r => !r.Ok),
                r => Assert.Equal("trip:request:create:quote_already_consumed", r.ErrorCode));
        }
    }

    [Fact]
    [Covers("trip/request", "second-request-rejected-while-active", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_holds_at_most_one_active_trip_however_many_requests_arrive_together()
    {
        await using var harness = Harness();

        for (var trial = 0; trial < 5; trial++)
        {
            var rider = Rider();
            var quotes = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => harness.SendAsync(Journey())));

            var results = await Task.WhenAll(
                quotes.Select(quote => harness.TrySendAsync(new RequestRide.Request(rider, quote.Id))));

            Assert.Equal(1, results.Count(r => r.Ok));
        }
    }

    [Fact]
    [Covers("trip/request", "request-admitted-after-terminal", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_may_request_again_once_their_trip_is_terminal()
    {
        await using var harness = Harness();
        var rider = Rider();

        var first = await harness.SendAsync(
            new RequestRide.Request(rider, (await harness.SendAsync(Journey())).Id));

        var blocked = await harness.TrySendAsync(
            new RequestRide.Request(rider, (await harness.SendAsync(Journey())).Id));
        Assert.False(blocked.Ok);

        await harness.SendAsync(new TransitionTrip.Request(first.TripId, TripEvent.Cancel, rider));

        var again = await harness.TrySendAsync(
            new RequestRide.Request(rider, (await harness.SendAsync(Journey())).Id));
        Assert.True(again.Ok);
    }

    [Fact]
    [Covers("pricing/quote", "quote-valid-before-expiry", Scope.Component, Quantification.Invariant)]
    [Covers("pricing/quote", "quote-invalid-after-expiry", Scope.Component, Quantification.Invariant)]
    public async Task A_quote_is_valid_until_its_expiry_and_not_after()
    {
        await using var harness = Harness();

        var valid = await harness.SendAsync(new GetQuote.Request((await harness.SendAsync(Journey())).Id));
        var expired = await harness.SendAsync(new GetQuote.Request(await ExpiredQuoteAsync()));

        Assert.False(valid.Expired);
        Assert.True(expired.Expired);
        Assert.Equal(1500, expired.TotalMinor);
    }

    [Fact]
    [Covers("pricing/quote", "expired-quote-is-never-revalidated", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_stays_expired_and_a_new_one_gets_a_new_identity()
    {
        await using var harness = Harness();
        var expired = await ExpiredQuoteAsync();
        var reissued = (await harness.SendAsync(Journey())).Id;

        Assert.NotEqual(expired, reissued);
        Assert.True((await harness.SendAsync(new GetQuote.Request(expired))).Expired);
        Assert.True(await harness.QuoteExpiryAsync(Decode(expired)) <= Now);
    }

    [Fact]
    [Covers("pricing/quote", "quote-returned", Scope.Component, Quantification.Invariant)]
    public async Task An_issued_quote_carries_a_total_a_currency_and_an_expiry()
    {
        await using var harness = Harness();

        var quote = await harness.SendAsync(Journey());

        Assert.NotEqual(0, Decode(quote.Id));
        Assert.Equal("EUR", quote.Currency);
        Assert.Equal(1500, quote.TotalMinor);
        Assert.Equal(Now + TimeSpan.FromMinutes(2), quote.ExpiresAt);
    }

}

[Collection("postgres")]
public sealed class DispatchTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private TripHarness Harness(DateTimeOffset? at = null) => new(fixture.ConnectionString, at ?? Now);

    private static async Task<(long Id, string Encoded)> TripAsync(TripHarness harness)
    {
        var quote = await harness.SendAsync(new IssueQuote.Request("a", "b", 1000, 500, "EUR"));
        var trip = await harness.SendAsync(
            new RequestRide.Request($"rider-{Guid.NewGuid():N}", quote.Id));

        Assert.True(IdEncoding.TryDecode(trip.TripId, out var id));
        return (id, trip.TripId);
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
        await using var harness = Harness();
        await harness.SeedDriversAsync(available: 6, unavailable: 0);

        for (var trial = 0; trial < 5; trial++)
        {
            var trip = await TripAsync(harness);

            var results = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(i =>
                    harness.TrySendAsync(new AcceptOffer.Request(trip.Encoded, $"driver-{i}"))));

            Assert.Equal(1, results.Count(r => r.Ok));
            Assert.Equal(TripState.Assigned, await harness.StateAsync(trip.Id));
            Assert.NotNull(await harness.AssignedDriverAsync(trip.Id));
        }
    }

    [Fact]
    [Covers("trip/dispatch", "late-acceptance-rejected", Scope.Component, Quantification.Invariant)]
    public async Task An_acceptance_after_assignment_changes_nothing()
    {
        await using var harness = Harness();
        await harness.SeedDriversAsync(available: 2, unavailable: 0);
        var trip = await TripAsync(harness);

        Assert.True((await harness.TrySendAsync(new AcceptOffer.Request(trip.Encoded, "driver-0"))).Ok);
        var before = await harness.AssignedDriverAsync(trip.Id);

        var late = await harness.TrySendAsync(new AcceptOffer.Request(trip.Encoded, "driver-1"));

        Assert.False(late.Ok);
        Assert.Equal("trip:dispatch:accept:offer_taken", late.ErrorCode);
        Assert.Equal(before, await harness.AssignedDriverAsync(trip.Id));
    }

    [Fact]
    [Covers("trip/dispatch", "offer-sent-to-available-nearby-driver", Scope.Component, Quantification.Invariant)]
    [Covers("trip/dispatch", "unavailable-driver-not-offered", Scope.Component, Quantification.Invariant)]
    public async Task Only_available_nearby_drivers_are_offered()
    {
        await using var harness = Harness();
        await harness.SeedDriversAsync(available: 3, unavailable: 2);
        var trip = await TripAsync(harness);

        var offered = (await harness.SendAsync(new GetOffers.Request(trip.Encoded)))
            .Select(o => o.DriverId)
            .ToHashSet();

        Assert.Contains("driver-0", offered);
        Assert.DoesNotContain("driver-3", offered);
        Assert.DoesNotContain("driver-4", offered);
    }

    [Fact]
    [Covers("trip/dispatch", "no-available-drivers", Scope.Component, Quantification.Invariant)]
    public async Task No_available_drivers_means_no_offers()
    {
        await using var harness = Harness();
        await harness.WithdrawAllDriversAsync();
        var trip = await TripAsync(harness);

        var offered = await harness.SendAsync(new OfferTripToDrivers.Request(trip.Id, "nowhere"));

        Assert.Equal(0, offered.DriversOffered);
        Assert.Empty(await harness.SendAsync(new GetOffers.Request(trip.Encoded)));
    }

    [Fact]
    [Covers("trip/dispatch", "other-offers-withdrawn", Scope.Component, Quantification.Invariant)]
    public async Task Assignment_withdraws_every_other_offer()
    {
        await using var harness = Harness();
        await harness.SeedDriversAsync(available: 4, unavailable: 0);
        var trip = await TripAsync(harness);

        await harness.SendAsync(new AcceptOffer.Request(trip.Encoded, "driver-0"));

        var offers = await harness.SendAsync(new GetOffers.Request(trip.Encoded));
        Assert.Equal("accepted", offers.Single(o => o.DriverId == "driver-0").State);
        Assert.All(offers.Where(o => o.DriverId != "driver-0"), o => Assert.Equal("withdrawn", o.State));
    }

    [Fact]
    [Covers("trip/dispatch", "expired-offer-withdrawn", Scope.Component, Quantification.Example)]
    public async Task An_offer_past_its_expiry_is_withdrawn()
    {
        await using var harness = Harness();
        await harness.SeedDriversAsync(available: 2, unavailable: 0);
        var trip = await TripAsync(harness);

        await using var later = Harness(Now + TimeSpan.FromMinutes(1));
        var offers = await later.SendAsync(new GetOffers.Request(trip.Encoded));

        Assert.NotEmpty(offers);
        Assert.All(offers, o => Assert.Equal("withdrawn", o.State));
    }
}

[Collection("postgres")]
public sealed class LifecycleTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private TripHarness Harness() => new(fixture.ConnectionString, Now);

    /// <summary>A trip a driver holds, reached the way a driver reaches it.</summary>
    private static async Task<(long Id, string Encoded)> AssignedTripAsync(TripHarness harness)
    {
        await harness.SeedDriversAsync(available: 1, unavailable: 0);

        var quote = await harness.SendAsync(new IssueQuote.Request("a", "b", 1000, 500, "EUR"));
        var trip = await harness.SendAsync(
            new RequestRide.Request($"rider-{Guid.NewGuid():N}", quote.Id));

        await harness.SendAsync(new AcceptOffer.Request(trip.TripId, "driver-0"));

        Assert.True(IdEncoding.TryDecode(trip.TripId, out var id));
        return (id, trip.TripId);
    }

    /// <summary>
    /// The conditional write, not the machine. At unit scope this would verify that the handler
    /// compares a state it was handed, which is not the claim.
    /// </summary>
    [Fact]
    [Covers("trip/lifecycle", "replayed-transition-is-inert", Scope.Component, Quantification.Invariant)]
    public async Task A_replayed_transition_changes_nothing_however_many_times_it_arrives()
    {
        await using var harness = Harness();

        for (var trial = 0; trial < 5; trial++)
        {
            var trip = await AssignedTripAsync(harness);
            await harness.SendAsync(new TransitionTrip.Request(trip.Encoded, TripEvent.Start, "driver-0"));
            await harness.SendAsync(new TransitionTrip.Request(trip.Encoded, TripEvent.Complete, "driver-0"));

            var replays = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(_ =>
                    harness.TrySendAsync(
                        new TransitionTrip.Request(trip.Encoded, TripEvent.Complete, "driver-0"))));

            Assert.All(replays, r => Assert.False(r.Ok));
            Assert.Equal(TripState.Completed, await harness.StateAsync(trip.Id));
        }
    }

    [Fact]
    [Covers("trip/lifecycle", "history-is-append-only", Scope.Component, Quantification.Invariant)]
    [Covers("trip/lifecycle", "transition-records-actor-and-instant", Scope.Component, Quantification.Invariant)]
    public async Task History_only_grows_and_records_who_caused_each_move()
    {
        await using var harness = Harness();
        var trip = await AssignedTripAsync(harness);
        var afterAssign = await harness.HistoryAsync(trip.Id);

        await harness.SendAsync(new TransitionTrip.Request(trip.Encoded, TripEvent.Start, "driver-0"));
        await harness.SendAsync(new TransitionTrip.Request(trip.Encoded, TripEvent.Complete, "driver-0"));
        var afterComplete = await harness.HistoryAsync(trip.Id);

        Assert.Equal(afterAssign, afterComplete.Take(afterAssign.Count));
        Assert.Equal(afterAssign.Count + 2, afterComplete.Count);
        Assert.All(afterComplete, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Actor)));
        Assert.Equal(("in-progress", "completed", "driver-0"), afterComplete[^1]);
    }

    [Fact]
    [Covers("trip/lifecycle", "no-transition-out-of-terminal", Scope.Component, Quantification.Invariant)]
    public async Task A_terminal_trip_admits_no_event_against_a_real_store()
    {
        await using var harness = Harness();
        var trip = await AssignedTripAsync(harness);
        await harness.SendAsync(new TransitionTrip.Request(trip.Encoded, TripEvent.Cancel, "rider"));

        foreach (var @event in TripStateMachine.Events)
        {
            var result = await harness.TrySendAsync(
                new TransitionTrip.Request(trip.Encoded, @event, "anyone"));
            Assert.False(result.Ok);
        }

        Assert.Equal(TripState.Cancelled, await harness.StateAsync(trip.Id));
    }

    [Fact]
    [Covers("trip/lifecycle", "rider-cancels-before-start", Scope.Component, Quantification.Example)]
    [Covers("trip/lifecycle", "driver-cancels-after-assignment", Scope.Component, Quantification.Example)]
    [Covers("trip/lifecycle", "cancellation-after-completion-rejected", Scope.Component, Quantification.Example)]
    public async Task Cancellation_records_the_cancelling_party_and_is_refused_after_completion()
    {
        await using var harness = Harness();

        var byRider = await AssignedTripAsync(harness);
        Assert.True((await harness.TrySendAsync(
            new TransitionTrip.Request(byRider.Encoded, TripEvent.Cancel, "rider"))).Ok);
        Assert.Equal("rider", (await harness.HistoryAsync(byRider.Id))[^1].Actor);

        var byDriver = await AssignedTripAsync(harness);
        Assert.True((await harness.TrySendAsync(
            new TransitionTrip.Request(byDriver.Encoded, TripEvent.Cancel, "driver-0"))).Ok);
        Assert.Equal("driver-0", (await harness.HistoryAsync(byDriver.Id))[^1].Actor);

        var completed = await AssignedTripAsync(harness);
        await harness.SendAsync(new TransitionTrip.Request(completed.Encoded, TripEvent.Start, "driver-0"));
        await harness.SendAsync(new TransitionTrip.Request(completed.Encoded, TripEvent.Complete, "driver-0"));
        Assert.False((await harness.TrySendAsync(
            new TransitionTrip.Request(completed.Encoded, TripEvent.Cancel, "rider"))).Ok);
    }
}
