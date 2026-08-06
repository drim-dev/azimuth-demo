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
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("trips/request", "request-admitted-with-valid-quote", Scope.Component, Quantification.Invariant)]
    [Covers("trips/request", "trip-created-in-requested-state", Scope.Component, Quantification.Invariant)]
    public async Task A_valid_quote_admits_a_request_and_creates_one_trip()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();

        var response = await client.RequestRide(rider, await client.QuoteId());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trip = await response.Read<RequestRide.Response>();
        trip.State.Should().Be("requested");
        trip.FareMinor.Should().Be(1500);
        trip.AwaitingDriver.Should().BeTrue();

        IdEncoding.TryDecode(trip.TripId, out var id).Should().BeTrue();
        var stored = await fixture.Database.SingleOrDefault<Trip>(t => t.Id == id, Cancellation.Token());
        stored.Should().NotBeNull();
        stored!.State.Should().Be(TripState.Requested);
        stored.RiderId.Should().Be(rider);
        stored.FareMinor.Should().Be(1500);
    }

    [Fact]
    [Covers("trips/request", "request-rejected-with-expired-quote", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_is_refused_and_creates_nothing()
    {
        var client = fixture.HttpClient.CreateClient();
        var quote = await client.QuoteId();

        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        var response = await client.RequestRide(Api.Rider(), quote);

        await response.ShouldBeBusinessRuleError("trip:request:create:expired_quote");
        (await fixture.Database.Count<Trip>(t => true, Cancellation.Token())).Should().Be(0);
    }

    [Fact]
    [Covers("trips/request", "request-rejected-with-unknown-quote", Scope.Component, Quantification.Invariant, Oracle.Contract)]
    public async Task An_unrecognised_quote_is_refused()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.RequestRide(Api.Rider(), IdEncoding.Encode(Random.Shared.NextInt64()));

        await response.ShouldBeBusinessRuleError("trip:request:create:unknown_quote");
        (await fixture.Database.Count<Trip>(t => true, Cancellation.Token())).Should().Be(0);
    }

    /// <summary>
    /// Quantified over concurrency: the sequential version of this passes against an implementation
    /// with no constraint at all, which is the whole reason the plan raises the scope.
    /// </summary>
    [Fact]
    [Covers("trips/request", "quote-consumed-once", Scope.Component, Quantification.Invariant)]
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

    [Fact]
    [Covers("trips/request", "second-request-rejected-while-active", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_holds_at_most_one_active_trip_however_many_requests_arrive_together()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            var rider = Api.Rider();
            var quotes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.QuoteId()));

            var responses = await Task.WhenAll(quotes.Select(q => client.RequestRide(rider, q)));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            (await fixture.Database.Count<Trip>(t => t.RiderId == rider, Cancellation.Token()))
                .Should().Be(1);
        }
    }

    [Fact]
    [Covers("trips/request", "request-admitted-after-terminal", Scope.Component, Quantification.Invariant)]
    public async Task A_rider_may_request_again_once_their_trip_is_terminal()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();

        var first = await (await client.RequestRide(rider, await client.QuoteId()))
            .Read<RequestRide.Response>();

        var blocked = await client.RequestRide(rider, await client.QuoteId());
        await blocked.ShouldBeBusinessRuleError("trip:request:create:rider_has_active_trip");

        (await client.Move(first.TripId, "cancel", rider)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await client.RequestRide(rider, await client.QuoteId());
        again.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public sealed class ValidatorTests
    {
        private readonly RequestRide.RequestValidator _validator = new();

        [Fact]
        [Untraced("shape only; the claims about admission are settled against real storage")]
        public void A_request_names_its_rider()
        {
            _validator.TestValidate(new RequestRide.Request(string.Empty, "0000000000000"))
                .ShouldHaveValidationErrorFor(x => x.RiderId);
        }

        [Fact]
        [Untraced("shape only; the claims about admission are settled against real storage")]
        public void A_request_names_its_quote()
        {
            _validator.TestValidate(new RequestRide.Request("rider-1", string.Empty))
                .ShouldHaveValidationErrorFor(x => x.QuoteId);
        }

        [Fact]
        [Untraced("the accepting half of the rules above")]
        public void A_well_formed_request_passes()
        {
            _validator.TestValidate(new RequestRide.Request("rider-1", "0000000000000"))
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
