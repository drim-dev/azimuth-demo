using System.Net;
using Azimuth.Annotations;
using Common.Identity;
using Common.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Trips.Domain;
using Trips.Features.Dispatch;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Dispatch;

[Collection(DispatchTestsCollection.Name)]
public sealed class AcceptOfferTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedDrivers(int available, int unavailable = 0)
    {
        var drivers = Enumerable.Range(0, available)
            .Select(i => Api.AvailableDriver($"driver-{i}"))
            .Concat(Enumerable.Range(available, unavailable)
                .Select(i => Api.UnavailableDriver($"driver-{i}")))
            .ToArray();

        await fixture.Database.Save(drivers);
    }

    private async Task<(long Id, string Encoded)> RequestedTrip(HttpClient client)
    {
        var encoded = await client.RideId();
        IdEncoding.TryDecode(encoded, out var id).Should().BeTrue();
        return (id, encoded);
    }

    /// <summary>
    /// The claim quantifies over "any number of drivers accepting concurrently". A test that
    /// accepts twice sequentially satisfies the words and not the claim.
    /// </summary>
    [Fact]
    [Covers("trips/dispatch", "concurrent-acceptances-yield-one-assignment", Scope.Component, Quantification.Invariant)]
    [Covers("trips/dispatch", "first-acceptance-assigns", Scope.Component, Quantification.Invariant)]
    public async Task Exactly_one_driver_is_assigned_however_many_accept_together()
    {
        var client = fixture.HttpClient.CreateClient();

        for (var trial = 0; trial < 5; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            await SeedDrivers(available: 6);
            var trip = await RequestedTrip(client);

            var responses = await Task.WhenAll(
                Enumerable.Range(0, 6).Select(i => client.Accept(trip.Encoded, $"driver-{i}")));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);

            var stored = await fixture.Database.SingleOrDefault<Trip>(
                t => t.Id == trip.Id, Cancellation.Token());
            stored!.State.Should().Be(TripState.Assigned);
            stored.AssignedDriverId.Should().NotBeNull();
        }
    }

    [Fact]
    [Covers("trips/dispatch", "late-acceptance-rejected", Scope.Component, Quantification.Invariant)]
    public async Task An_acceptance_after_assignment_changes_nothing()
    {
        var client = fixture.HttpClient.CreateClient();
        await SeedDrivers(available: 2);
        var trip = await RequestedTrip(client);

        (await client.Accept(trip.Encoded, "driver-0")).StatusCode.Should().Be(HttpStatusCode.OK);
        var before = (await fixture.Database.SingleOrDefault<Trip>(
            t => t.Id == trip.Id, Cancellation.Token()))!.AssignedDriverId;

        var late = await client.Accept(trip.Encoded, "driver-1");

        await late.ShouldBeConflict("trip:dispatch:accept:offer_taken");
        var after = (await fixture.Database.SingleOrDefault<Trip>(
            t => t.Id == trip.Id, Cancellation.Token()))!.AssignedDriverId;
        after.Should().Be(before);
    }

    [Fact]
    [Covers("trips/dispatch", "other-offers-withdrawn", Scope.Component, Quantification.Invariant)]
    public async Task Assignment_withdraws_every_other_offer()
    {
        var client = fixture.HttpClient.CreateClient();
        await SeedDrivers(available: 4);
        var trip = await RequestedTrip(client);

        await client.Accept(trip.Encoded, "driver-0");

        var offers = await (await client.GetOffers(trip.Encoded))
            .Read<IReadOnlyList<GetOffers.Offered>>();

        offers.Single(o => o.DriverId == "driver-0").State.Should().Be("accepted");
        offers.Where(o => o.DriverId != "driver-0").Should().OnlyContain(o => o.State == "withdrawn");

        var stored = await fixture.Database.Execute(async db =>
            await db.Offers.AsNoTracking().Where(o => o.TripId == trip.Id).ToListAsync());
        stored.Count(o => o.State == OfferState.Accepted).Should().Be(1);
    }
}
