using System.Net;
using Azimuth.Annotations;
using Common.Identity;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Trips.Domain;
using Trips.Features.Trips;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Trips;

[Collection(LifecycleTestsCollection.Name)]
public sealed class TransitionTripTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A trip a driver holds, reached the way a driver reaches it.</summary>
    private async Task<(long Id, string Encoded)> AssignedTrip(HttpClient client)
    {
        await fixture.Database.Save(Api.AvailableDriver("driver-0"));

        var encoded = await client.RideId();
        (await client.Accept(encoded, "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);

        IdEncoding.TryDecode(encoded, out var id).Should().BeTrue();
        return (id, encoded);
    }

    private Task<TripState?> State(long id) => fixture.Database.Execute(async db =>
        await db.Trips.AsNoTracking().Where(t => t.Id == id).Select(t => (TripState?)t.State)
            .FirstOrDefaultAsync());

    private Task<List<(string From, string To, string Actor)>> History(long id) =>
        fixture.Database.Execute(async db =>
        {
            var rows = await db.TripTransitions.AsNoTracking()
                .Where(t => t.TripId == id)
                .OrderBy(t => t.Id)
                .Select(t => new { t.FromState, t.ToState, t.Actor })
                .ToListAsync();

            return rows
                .Select(r => (TripStateMachine.Name(r.FromState), TripStateMachine.Name(r.ToState), r.Actor))
                .ToList();
        });

    /// <summary>
    /// The conditional write, not the machine. At unit scope this would verify that the handler
    /// compares a state it was handed, which is not the claim.
    /// </summary>
    [Fact]
    [Covers("trips/lifecycle", "replayed-transition-is-inert", Scope.Component, Quantification.Universal)]
    public async Task A_replayed_transition_changes_nothing_however_many_times_it_arrives()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            var trip = await AssignedTrip(client);
            await client.Move(trip.Encoded, "start", "driver-0");
            await client.Move(trip.Encoded, "complete", "driver-0");

            var replays = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(_ => client.Move(trip.Encoded, "complete", "driver-0")));

            replays.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
            (await State(trip.Id)).Should().Be(TripState.Completed);

            // One completion in the history, however many arrived.
            (await History(trip.Id)).Count(e => e.To == "completed").Should().Be(1);
            (await fixture.Database.Count<TripEventOutbox>(
                x => x.TripId == trip.Id,
                Cancellation.Token())).Should().Be(4);
        }
    }

    [Fact]
    [Covers("trips/lifecycle", "history-is-append-only", Scope.Component, Quantification.Example)]
    [Covers("trips/lifecycle", "transition-records-actor-and-instant", Scope.Component, Quantification.Example)]
    public async Task History_only_grows_and_records_who_caused_each_move()
    {
        var client = fixture.HttpClient.CreateClient();
        var trip = await AssignedTrip(client);
        var afterAssign = await History(trip.Id);

        await client.Move(trip.Encoded, "start", "driver-0");
        await client.Move(trip.Encoded, "complete", "driver-0");
        var afterComplete = await History(trip.Id);

        afterComplete.Take(afterAssign.Count).Should().Equal(afterAssign);
        afterComplete.Should().HaveCount(afterAssign.Count + 2);
        afterComplete.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Actor));
        afterComplete[^1].Should().Be(("in-progress", "completed", "driver-0"));

        var instants = await fixture.Database.Execute(async db =>
            await db.TripTransitions.AsNoTracking()
                .Where(t => t.TripId == trip.Id)
                .Select(t => t.OccurredAt)
                .ToListAsync());
        instants.Should().OnlyContain(at => at == TripTestFixture.Start);
    }

    [Fact]
    [Covers("trips/lifecycle", "no-transition-out-of-terminal", Scope.Component, Quantification.Example)]
    public async Task A_terminal_trip_admits_no_event_against_a_real_store()
    {
        var client = fixture.HttpClient.CreateClient();
        var trip = await AssignedTrip(client);
        await client.Move(trip.Encoded, "cancel", "rider");

        foreach (var verb in new[] { "start", "complete", "cancel" })
        {
            var response = await client.Move(trip.Encoded, verb, "anyone");
            await response.ShouldBeConflict("trip:trip:transition:not_permitted");
        }

        (await State(trip.Id)).Should().Be(TripState.Cancelled);
    }

    [Fact]
    [Covers("trips/lifecycle", "rider-cancels-before-start", Scope.Component, Quantification.Example)]
    [Covers("trips/lifecycle", "driver-cancels-after-assignment", Scope.Component, Quantification.Example)]
    [Covers("trips/lifecycle", "cancellation-after-completion-rejected", Scope.Component, Quantification.Example)]
    public async Task Cancellation_records_the_cancelling_party_and_is_refused_after_completion()
    {
        var client = fixture.HttpClient.CreateClient();

        var byRider = await AssignedTrip(client);
        (await client.Move(byRider.Encoded, "cancel", "rider")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await History(byRider.Id))[^1].Actor.Should().Be("rider");

        await fixture.Reset(Cancellation.Token());
        var byDriver = await AssignedTrip(client);
        (await client.Move(byDriver.Encoded, "cancel", "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await History(byDriver.Id))[^1].Actor.Should().Be("driver-0");

        await fixture.Reset(Cancellation.Token());
        var completed = await AssignedTrip(client);
        await client.Move(completed.Encoded, "start", "driver-0");
        await client.Move(completed.Encoded, "complete", "driver-0");
        await (await client.Move(completed.Encoded, "cancel", "rider"))
            .ShouldBeConflict("trip:trip:transition:not_permitted");
    }

    [Fact]
    public async Task An_unknown_trip_admits_no_event()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.Move("0000000000000", "start", "anyone");

        await response.ShouldBeNotFound("trip:trip:transition:not_found");
    }

    public sealed class ValidatorTests
    {
        private readonly TransitionTrip.RequestValidator _validator = new();

        [Fact]
        public void A_transition_names_the_party_that_caused_it()
        {
            _validator.TestValidate(new TransitionTrip.Request("0000000000000", TripEvent.Start, string.Empty))
                .ShouldHaveValidationErrorFor(x => x.Actor);
        }

        [Fact]
        public void A_transition_with_an_actor_passes()
        {
            _validator.TestValidate(new TransitionTrip.Request("0000000000000", TripEvent.Start, "rider"))
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
