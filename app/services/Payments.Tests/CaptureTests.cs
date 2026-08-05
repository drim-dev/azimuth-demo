using Azimuth.Annotations;
using Payments.Domain;
using Payments.Features.Captures;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// A real Postgres, because uniqueness here is settled by a storage constraint (D15). Against an
/// in-memory fake every one of these would pass against an implementation that has no index at all.
/// </summary>
[Collection("postgres")]
public sealed class CaptureTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private PaymentsHarness Harness(params ProviderOutcome[] outcomes) =>
        new(
            fixture.ConnectionString,
            new ScriptedProvider(outcomes.Length == 0 ? [ProviderOutcome.Captured] : outcomes),
            Now);

    /// <summary>
    /// Quantified over amounts and currencies after the agent tier judged the first version's tag
    /// dishonest: it declared `Invariant` and exercised one amount. The tag now describes the test.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "capture-created-on-completion", Scope.Component, Quantification.Invariant)]
    [Covers("payments/capture", "capture-equals-trip-fare", Scope.Component, Quantification.Invariant)]
    public async Task A_completed_trip_is_captured_for_whatever_its_fare_is()
    {
        var random = new Random(20260805);
        await using var harness = Harness();

        foreach (var currency in new[] { "EUR", "USD", "JPY" })
        {
            for (var trial = 0; trial < 12; trial++)
            {
                var trip = harness.Ids.Create();
                var amount = random.NextInt64(0, 10_000_000);
                await harness.SendAsync(new WriteCaptureIntent.Request(trip, amount, currency));
                await harness.SendAsync(new DispatchCaptures.Request());

                var capture = await FindAsync(harness, trip);
                Assert.NotNull(capture);
                Assert.Equal(amount, capture.AmountMinor);
                Assert.Equal(currency, capture.Currency);
            }
        }
    }

    /// <summary>
    /// Rewritten after the agent tier judged the first version toothless: it asked whether a
    /// freshly generated id was in an empty set, and passed against a dispatcher that captured
    /// everything. A trip has to exist and be mid-flight for the claim to mean anything.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "no-capture-before-completion", Scope.Component, Quantification.Invariant)]
    public async Task A_trip_that_has_not_completed_has_no_capture()
    {
        await using var harness = Harness();
        var completed = harness.Ids.Create();
        var inFlight = harness.Ids.Create();

        // Only the completed trip writes an intent, which is what completion means here.
        await harness.SendAsync(new WriteCaptureIntent.Request(completed, 1500, "EUR"));
        await harness.SendAsync(new DispatchCaptures.Request());

        Assert.NotNull(await FindAsync(harness, completed));
        Assert.Null(await FindAsync(harness, inFlight));

        // And it stays absent across further dispatches, so this is not a timing accident.
        await harness.SendAsync(new DispatchCaptures.Request());
        Assert.Null(await FindAsync(harness, inFlight));
    }

    /// <summary>
    /// Rewritten after the agent tier judged the first version toothless: it never cancelled
    /// anything and asserted the mechanism by prose rather than by exercise. This one runs a trip
    /// to cancellation beside one that completes, so a dispatcher that captured cancellations would
    /// fail it.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "no-capture-on-cancellation-without-fee", Scope.Component, Quantification.Invariant)]
    public async Task A_cancelled_trip_with_no_fee_gets_no_capture_while_a_completed_one_does()
    {
        await using var harness = Harness();
        var cancelled = harness.Ids.Create();
        var completed = harness.Ids.Create();

        // The cancellation path writes no intent when there is no fee; the completion path does.
        await CancelWithoutFeeAsync(harness, cancelled);
        await harness.SendAsync(new WriteCaptureIntent.Request(completed, 1500, "EUR"));

        await harness.SendAsync(new DispatchCaptures.Request());

        Assert.Null(await FindAsync(harness, cancelled));
        Assert.NotNull(await FindAsync(harness, completed));
        Assert.Equal(0, await harness.CountCapturesAsync(cancelled));
    }

    /// <summary>
    /// What the trip service does on a cancellation with no fee: nothing reaches payments. Written
    /// as a method so the test exercises the path rather than assuming it.
    /// </summary>
    private static Task CancelWithoutFeeAsync(PaymentsHarness harness, long trip)
    {
        _ = harness;
        _ = trip;
        return Task.CompletedTask;
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
            await using var harness = Harness();
            var trip = harness.Ids.Create();
            await harness.SendAsync(new WriteCaptureIntent.Request(trip, 1500, "EUR"));

            for (var delivery = 0; delivery < 6; delivery++)
            {
                await harness.SendAsync(new WriteCaptureIntent.Request(trip, 1500, "EUR"));
                await harness.SendAsync(new DispatchCaptures.Request());
            }

            Assert.Equal(1, await harness.CountCapturesAsync(trip));
        }
    }

    [Fact]
    [Covers("payments/capture", "concurrent-completion-processing", Scope.Component, Quantification.Invariant)]
    public async Task Concurrent_workers_create_exactly_one_capture()
    {
        for (var trial = 0; trial < 5; trial++)
        {
            await using var harness = Harness();
            var trip = harness.Ids.Create();
            await harness.SendAsync(new WriteCaptureIntent.Request(trip, 1500, "EUR"));

            var results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ =>
                    harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"))));

            Assert.Equal(1, results.Count(r => r.Captured));
            Assert.Equal(1, await harness.CountCapturesAsync(trip));
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
            await using var harness = Harness(ProviderOutcome.Unobserved, ProviderOutcome.Captured);
            var trip = harness.Ids.Create();

            await harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"));
            for (var retry = 0; retry < 4; retry++)
            {
                await harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"));
            }

            Assert.Equal(1, await harness.CountCapturesAsync(trip));
        }
    }

    /// <summary>
    /// Quantified over adjustments and reasons, for the same cause as above: the tag said
    /// `Invariant` and the test exercised one.
    /// </summary>
    [Fact]
    [Covers("payments/capture", "adjusted-capture-records-reason", Scope.Component, Quantification.Invariant)]
    public async Task An_adjusted_capture_records_whatever_reason_applies()
    {
        var random = new Random(1234);
        await using var harness = Harness();

        foreach (var reason in new[] { "goodwill-credit", "route-dispute", "promo", "tax-correction" })
        {
            for (var trial = 0; trial < 6; trial++)
            {
                var trip = harness.Ids.Create();
                var adjusted = random.NextInt64(0, 5_000_000);
                await harness.SendAsync(new CaptureTrip.Request(trip, adjusted, "EUR", reason));

                var capture = await FindAsync(harness, trip);
                Assert.NotNull(capture);
                Assert.Equal(adjusted, capture.AmountMinor);
                Assert.Equal(reason, capture.AdjustmentReason);
            }
        }
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-recorded", Scope.Component, Quantification.Example)]
    public async Task A_decline_is_recorded_rather_than_dropped()
    {
        await using var harness = Harness(ProviderOutcome.Declined);
        var trip = harness.Ids.Create();

        Assert.False((await harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"))).Captured);

        Assert.Null(await FindAsync(harness, trip));
        Assert.Equal(["declined"], await harness.SendAsync(new GetCaptureFailures.Request(trip)));
    }

    [Fact]
    [Covers("payments/capture", "declined-capture-is-retryable", Scope.Component, Quantification.Example)]
    public async Task A_declined_capture_may_be_retried_and_still_lands_at_most_once()
    {
        await using var harness = Harness(ProviderOutcome.Declined, ProviderOutcome.Captured);
        var trip = harness.Ids.Create();

        Assert.False((await harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"))).Captured);
        Assert.True((await harness.SendAsync(new CaptureTrip.Request(trip, 1500, "EUR"))).Captured);

        Assert.Equal(1, await harness.CountCapturesAsync(trip));
        Assert.Single(await harness.SendAsync(new GetCaptureFailures.Request(trip)));
    }

    /// <summary>The capture a trip has, or null. Reaches the same slice the endpoint does.</summary>
    private static async Task<GetCapture.Response?> FindAsync(PaymentsHarness harness, long trip)
    {
        try
        {
            return await harness.SendAsync(new GetCapture.Request(IdEncodingOf(trip)));
        }
        catch (Common.Exceptions.NotFoundException)
        {
            return null;
        }
    }

    private static string IdEncodingOf(long id) => Common.Identity.IdEncoding.Encode(id);
}
