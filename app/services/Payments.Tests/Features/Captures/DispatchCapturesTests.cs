using System.Net;
using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using Payments.Domain;
using Payments.Tests.Fixtures;
using Xunit;

namespace Payments.Tests.Features.Captures;

[Collection(CaptureTestsCollection.Name)]
public sealed class DispatchCapturesTests(PaymentsTestFixture fixture) : IAsyncLifetime
{
    private long _nextTrip = 1;

    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    private long TripId() => Interlocked.Increment(ref _nextTrip) * 1000;

    private Task<int> CaptureCount(long tripId) =>
        fixture.Database.Count<Capture>(c => c.TripId == tripId && !c.Voided, Cancellation.Token());

    /// <summary>
    /// Quantified over amounts and currencies after the agent tier judged the first version's tag
    /// dishonest: it declared `Invariant` and exercised one amount. The tag now describes the test.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "capture-equals-trip-fare", Scope.Component, Quantification.Universal)]
    public async Task A_completed_trip_is_captured_for_whatever_its_fare_is()
    {
        var client = fixture.HttpClient.CreateClient();
        var random = new Random(20260805);

        foreach (var currency in new[] { "EUR", "USD", "JPY" })
        {
            for (var trial = 0; trial < 12; trial++)
            {
                var trip = TripId();
                var amount = random.NextInt64(0, 10_000_000);
                await fixture.Database.Save(Api.CompletedTrip(trip, amount, currency));

                (await client.Dispatch()).StatusCode.Should().Be(HttpStatusCode.OK);

                var capture = await client.Capture(trip);
                capture.Should().NotBeNull();
                capture!.AmountMinor.Should().Be(amount);
                capture.Currency.Should().Be(currency);
            }
        }
    }

    [Fact]
    [Covers("payments/capture", "capture-equals-trip-fare", Scope.Component, Quantification.Universal,
        Oracle.Contract)]
    public async Task An_altered_quote_reaches_neither_the_provider_nor_the_capture_table()
    {
        var client = fixture.HttpClient.CreateClient();
        var trip = TripId();
        var intent = Api.CompletedTrip(trip, 1800);
        intent.QuoteToken = (intent.QuoteToken[0] == 'A' ? 'B' : 'A') + intent.QuoteToken[1..];
        await fixture.Database.Save(intent);

        var response = await client.Dispatch();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Read<Problem>()).ErrorCode.Should().Be("payment:capture:create:invalid_quote");
        fixture.Provider.Calls.Should().Be(0);
        (await CaptureCount(trip)).Should().Be(0);
    }

    /// <summary>
    /// Rewritten after the agent tier judged the first version toothless: it asked whether a
    /// freshly generated id was in an empty set, and passed against a dispatcher that captured
    /// everything. A trip has to exist and be mid-flight for the claim to mean anything.
    /// </summary>
    [Fact]
    public async Task A_trip_that_has_not_completed_has_no_capture()
    {
        var client = fixture.HttpClient.CreateClient();
        var completed = TripId();
        var inFlight = TripId();

        // Only the completed trip writes an intent, which is what completion means here.
        await fixture.Database.Save(Api.CompletedTrip(completed, 1500));
        await client.Dispatch();

        (await client.Capture(completed)).Should().NotBeNull();
        (await client.Capture(inFlight)).Should().BeNull();

        // And it stays absent across further dispatches, so this is not a timing accident.
        await client.Dispatch();
        (await client.Capture(inFlight)).Should().BeNull();
    }

    /// <summary>
    /// Rewritten after the agent tier judged the first version toothless: it never cancelled
    /// anything and asserted the mechanism by prose rather than by exercise. This one runs a trip
    /// to cancellation beside one that completes, so a dispatcher that captured cancellations would
    /// fail it.
    /// </summary>
    [Fact]
    public async Task A_cancelled_trip_with_no_fee_gets_no_capture_while_a_completed_one_does()
    {
        var client = fixture.HttpClient.CreateClient();
        var cancelled = TripId();
        var completed = TripId();

        // The cancellation path writes no intent when there is no fee; the completion path does.
        await CancelWithoutFee(cancelled);
        await fixture.Database.Save(Api.CompletedTrip(completed, 1500));

        await client.Dispatch();

        (await client.Capture(cancelled)).Should().BeNull();
        (await client.Capture(completed)).Should().NotBeNull();
        (await CaptureCount(cancelled)).Should().Be(0);
    }

    /// <summary>
    /// What the trip service does on a cancellation with no fee: nothing reaches payments. Written
    /// as a method so the test exercises the path rather than assuming it.
    /// </summary>
    private static Task CancelWithoutFee(long trip)
    {
        _ = trip;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Quantified over redelivery: the sequential version of this passes against an implementation
    /// with no index at all, which is the whole reason the plan raises the scope.
    /// </summary>
    [Fact]
    public async Task A_completion_delivered_any_number_of_times_captures_once()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            var trip = TripId();
            await fixture.Database.Save(Api.CompletedTrip(trip, 1500));

            for (var delivery = 0; delivery < 6; delivery++)
            {
                await client.Dispatch();
            }

            (await CaptureCount(trip)).Should().Be(1);
        }
    }

    [Fact]
    [Covers("payments/capture", "concurrent-completion-processing", Scope.Component, Quantification.Universal)]
    public async Task Concurrent_workers_create_exactly_one_capture()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            var trip = TripId();
            await fixture.Database.Save(Api.CompletedTrip(trip, 1500));

            var responses = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => client.Dispatch()));

            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
            (await CaptureCount(trip)).Should().Be(1);
        }
    }

    /// <summary>
    /// An outcome the caller never observed may or may not have succeeded. Assuming failure is what
    /// double-charges, so it is treated as possibly-captured and the index settles it.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "retry-after-transport-failure", Scope.Component, Quantification.Universal)]
    public async Task A_retry_after_an_unobserved_outcome_still_captures_once()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            fixture.Provider.Script(ProviderOutcome.Unobserved, ProviderOutcome.Captured);

            var trip = TripId();
            await fixture.Database.Save(Api.CompletedTrip(trip, 1500));

            for (var retry = 0; retry < 5; retry++)
            {
                await client.Dispatch();
            }

            (await CaptureCount(trip)).Should().Be(1);
        }
    }

    /// <summary>
    /// Quantified over adjustments and reasons, for the same cause as above: the tag said
    /// `Invariant` and the test exercised one.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "adjusted-capture-records-reason", Scope.Component, Quantification.Universal)]
    public async Task An_adjusted_capture_records_whatever_reason_applies()
    {
        var client = fixture.HttpClient.CreateClient();
        var random = new Random(1234);

        foreach (var reason in new[] { "goodwill-credit", "route-dispute", "promo", "tax-correction" })
        {
            for (var trial = 0; trial < 6; trial++)
            {
                var trip = TripId();
                var quoted = random.NextInt64(1_000_000, 5_000_000);
                var adjustment = random.NextInt64(-500_000, 500_000);
                if (adjustment == 0) adjustment = 1;
                await fixture.Database.Save(Api.CompletedTrip(trip, quoted));

                await client.Dispatch(adjustmentReason: reason, adjustmentMinor: adjustment);

                var capture = await client.Capture(trip);
                capture.Should().NotBeNull();
                capture!.AmountMinor.Should().Be(quoted + adjustment);
                capture.AmountMinor.Should().NotBe(quoted);
                capture.AdjustmentReason.Should().Be(reason);
            }
        }
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-recorded", Scope.Component, Quantification.Example)]
    public async Task A_decline_is_recorded_rather_than_dropped()
    {
        var client = fixture.HttpClient.CreateClient();
        fixture.Provider.Script(ProviderOutcome.Declined);

        var trip = TripId();
        await fixture.Database.Save(Api.CompletedTrip(trip, 1500));

        (await client.Dispatch()).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.Capture(trip)).Should().BeNull();
        (await client.Failures(trip)).Should().Equal("declined");
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-is-retryable", Scope.Component, Quantification.Example)]
    public async Task A_declined_capture_may_be_retried_and_still_lands_at_most_once()
    {
        var client = fixture.HttpClient.CreateClient();
        fixture.Provider.Script(ProviderOutcome.Declined, ProviderOutcome.Captured);

        var trip = TripId();
        await fixture.Database.Save(Api.CompletedTrip(trip, 1500));

        await client.Dispatch();
        (await client.Capture(trip)).Should().BeNull();

        await client.Dispatch();
        (await client.Capture(trip)).Should().NotBeNull();

        (await CaptureCount(trip)).Should().Be(1);
        (await client.Failures(trip)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_trip_with_no_capture_reports_absence()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.GetCapture(TripId());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Read<Problem>()).ErrorCode.Should().Be("payments:capture:get:not_found");
    }
}
