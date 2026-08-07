using System.Net;
using Azimuth.Annotations;
using Common.Identity;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Trips.Domain;
using Trips.Features.Trips;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Trips;

[Collection(AdmissionTestsCollection.Name)]
public sealed class RequestRideTests(TripTestFixture fixture) : IAsyncLifetime
{
    private static readonly string[] Currencies = ["EUR", "USD", "JPY"];

    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Ranges over the fare, because that is what the claim is about: the expected total is read
    /// back from the quote rather than written into the test, so a handler returning a constant
    /// fails. The count is asserted too — "exactly one trip" is a count, not the existence of the
    /// one the response named.
    /// </summary>
    [Fact]
    [Covers("trips/request", "request-admitted-with-valid-quote", Scope.Component, Quantification.Universal)]
    [Covers("trips/request", "trip-created-in-requested-state", Scope.Component, Quantification.Universal)]
    public async Task A_valid_quote_admits_a_request_and_creates_one_trip_carrying_its_total()
    {
        var client = fixture.HttpClient.CreateClient();
        var random = new Random(20260807);
        var admitted = 0;

        foreach (var currency in Currencies)
        {
            for (var trial = 0; trial < 8; trial++)
            {
                var quote = await client.Quote(
                    baseMinor: random.NextInt64(0, 500_000),
                    distanceMinor: random.NextInt64(0, 500_000),
                    currency: currency);
                var rider = Api.Rider();

                var response = await client.RequestRide(rider, quote.Id);

                response.StatusCode.Should().Be(HttpStatusCode.OK);
                var trip = await response.Read<RequestRide.Response>();
                trip.State.Should().Be("requested");
                trip.AwaitingDriver.Should().BeTrue();
                trip.FareMinor.Should().Be(quote.TotalMinor);
                trip.Currency.Should().Be(quote.Currency);

                IdEncoding.TryDecode(trip.TripId, out var id).Should().BeTrue();
                var stored = await fixture.Database.SingleOrDefault<Trip>(
                    t => t.Id == id,
                    Cancellation.Token());

                stored.Should().NotBeNull();
                stored!.State.Should().Be(TripState.Requested);
                stored.RiderId.Should().Be(rider);
                stored.FareMinor.Should().Be(quote.TotalMinor);
                stored.Currency.Should().Be(quote.Currency);

                admitted++;
                (await fixture.Database.Count<Trip>(t => true, Cancellation.Token()))
                    .Should().Be(admitted);
            }
        }
    }

    /// <summary>
    /// Ranges over the distance past expiry and constructs both sides of the boundary. A single
    /// sample deep in the expired region passes against a handler carrying a grace window, which is
    /// the mutation this claim exists to exclude.
    /// </summary>
    [Fact]
    [Covers("trips/request", "request-rejected-with-expired-quote", Scope.Component, Quantification.Universal)]
    public async Task An_expired_quote_is_refused_however_far_past_expiry()
    {
        var client = fixture.HttpClient.CreateClient();

        TimeSpan[] pastExpiry =
        [
            TimeSpan.Zero,
            TimeSpan.FromTicks(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromHours(1),
            TimeSpan.FromDays(1),
        ];

        foreach (var offset in pastExpiry)
        {
            await fixture.Reset(Cancellation.Token());
            var quote = await client.Quote();

            fixture.Clock.Advance(quote.ExpiresAt - TripTestFixture.Start + offset);
            var response = await client.RequestRide(Api.Rider(), quote.Id);

            await response.ShouldBeBusinessRuleError("trip:request:create:expired_quote");
            (await fixture.Database.Count<Trip>(t => true, Cancellation.Token())).Should().Be(0);
        }

        // The near side of the same instant. Without it, an implementation that expired quotes five
        // minutes late — or one that expired them immediately — passes everything above.
        await fixture.Reset(Cancellation.Token());
        var live = await client.Quote();

        fixture.Clock.Advance(live.ExpiresAt - TripTestFixture.Start - TimeSpan.FromTicks(1));

        (await client.RequestRide(Api.Rider(), live.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Ranges over malformed and cryptographically altered tokens. Both have to reach the rider as
    /// the same refusal, because the rider cannot distinguish corruption from forgery.
    /// </summary>
    [Fact]
    [Covers("trips/request", "request-rejected-with-unknown-quote", Scope.Component, Quantification.Universal)]
    public async Task An_unrecognised_quote_is_refused_whatever_identifier_is_offered()
    {
        var client = fixture.HttpClient.CreateClient();
        var random = new Random(20260807);
        var signed = (await client.Quote()).Id;
        var altered = (signed[0] == 'A' ? 'B' : 'A') + signed[1..];

        string[] identifiers =
        [
            altered,
            .. Enumerable.Range(0, 12).Select(_ => IdEncoding.Encode(random.NextInt64(1, long.MaxValue))),
            "not-an-id",
            "0",
            "!!!!!!!!!!!!!",
            new string('Z', 13),
        ];

        foreach (var identifier in identifiers)
        {
            var response = await client.RequestRide(Api.Rider(), identifier);
            await response.ShouldBeBusinessRuleError("trip:request:create:unknown_quote");
        }

        (await fixture.Database.Count<Trip>(t => true, Cancellation.Token())).Should().Be(0);
    }

    /// <summary>
    /// Quantified over concurrency: the sequential version of this passes against an implementation
    /// with no constraint at all, which is the whole reason the plan raises the scope. Eight
    /// distinct riders, so the per-rider index cannot pass the test on the quote rule's behalf.
    /// </summary>
    [Fact]
    [Covers("trips/request", "quote-consumed-once", Scope.Component, Quantification.Universal)]
    public async Task A_quote_is_consumed_by_at_most_one_request_however_many_arrive_together()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            var quote = await client.QuoteId();
            var riders = Enumerable.Range(0, 8).Select(_ => Api.Rider()).ToArray();

            var responses = await Task.WhenAll(riders.Select(r => client.RequestRide(r, quote)));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
            {
                await refused.ShouldBeBusinessRuleError("trip:request:create:quote_already_consumed");
            }
        }

        (await fixture.Database.Count<Trip>(t => true, Cancellation.Token())).Should().Be(5);
    }

    /// <summary>
    /// Eight distinct quotes, so the quote rule cannot pass the test on the per-rider index's
    /// behalf. The refusals are inspected rather than only counted: a rejection for the wrong
    /// reason is not this claim being satisfied.
    /// </summary>
    [Fact]
    [Covers("trips/request", "second-request-rejected-while-active", Scope.Component, Quantification.Universal)]
    public async Task A_rider_holds_at_most_one_active_trip_however_many_requests_arrive_together()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            var rider = Api.Rider();
            var quotes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.QuoteId()));

            var responses = await Task.WhenAll(quotes.Select(q => client.RequestRide(rider, q)));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
            {
                await refused.ShouldBeBusinessRuleError("trip:request:create:rider_has_active_trip");
            }

            (await fixture.Database.Count<Trip>(t => t.RiderId == rider, Cancellation.Token()))
                .Should().Be(1);
        }
    }

    /// <summary>
    /// The terminal set is derived from the state machine rather than listed here, so a third
    /// terminal state is covered the day it is added — or fails loudly for want of a path to it,
    /// which is the outcome a hand-written list cannot produce. The index this rests on names its
    /// terminal states in a SQL string, and dropping one from that filter is invisible to a test
    /// that only ever cancels.
    /// </summary>
    [Fact]
    [Covers("trips/request", "request-admitted-after-terminal", Scope.Component, Quantification.Universal)]
    public async Task A_rider_may_request_again_from_any_terminal_state()
    {
        var client = fixture.HttpClient.CreateClient();

        foreach (var terminal in TripStateMachine.States.Where(TripStateMachine.IsTerminal))
        {
            await fixture.Reset(Cancellation.Token());
            await fixture.Database.Save(Api.AvailableDriver("driver-0"));

            var rider = Api.Rider();
            var first = await (await client.RequestRide(rider, (await client.Quote()).Id))
                .Read<RequestRide.Response>();

            await DriveTo(client, first.TripId, terminal);

            var held = await fixture.Database.SingleOrDefault<Trip>(
                t => t.RiderId == rider,
                Cancellation.Token());
            held!.State.Should().Be(terminal);

            var again = await client.RequestRide(rider, (await client.Quote()).Id);

            again.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// The path to each terminal state, which is the part that cannot be derived. An unhandled
    /// member of the enumeration above fails rather than being silently skipped — the enumeration
    /// is the claim's domain, and a member with no path is a gap in the evidence, not in the rule.
    /// </summary>
    private static async Task DriveTo(HttpClient client, string tripId, TripState terminal)
    {
        switch (terminal)
        {
            case TripState.Cancelled:
                (await client.Move(tripId, "cancel", "rider")).StatusCode.Should().Be(HttpStatusCode.OK);
                break;

            case TripState.Completed:
                (await client.Accept(tripId, "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);
                (await client.Move(tripId, "start", "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);
                (await client.Move(tripId, "complete", "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);
                break;

            default:
                throw new NotSupportedException(
                    $"{terminal} is terminal and this test has no path to it. Add the path rather " +
                    "than narrowing the enumeration, which is what makes the tag true.");
        }
    }

    public sealed class ValidatorTests
    {
        private readonly RequestRide.RequestValidator _validator = new();

        [Fact]
        public void A_request_names_its_rider()
        {
            _validator.TestValidate(new RequestRide.Request(string.Empty, "0000000000000"))
                .ShouldHaveValidationErrorFor(x => x.RiderId);
        }

        [Fact]
        public void A_request_names_its_quote()
        {
            _validator.TestValidate(new RequestRide.Request("rider-1", string.Empty))
                .ShouldHaveValidationErrorFor(x => x.QuoteToken);
        }

        [Fact]
        public void A_well_formed_request_passes()
        {
            _validator.TestValidate(new RequestRide.Request("rider-1", "0000000000000"))
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
