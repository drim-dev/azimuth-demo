using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using Trips.Domain;
using Trips.Features.Dispatch;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Dispatch;

[Collection(DispatchTestsCollection.Name)]
public sealed class GetOffersTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<IReadOnlyList<GetOffers.Offered>> Offers(HttpClient client, string tripId) =>
        client.GetOffers(tripId).ContinueWith(t => t.Result.Read<IReadOnlyList<GetOffers.Offered>>()).Unwrap();

    /// <summary>
    /// Fan-out happens because a trip was admitted, so the offers are a side effect of requesting a
    /// ride rather than something a caller triggers.
    /// </summary>
    [Fact]
    [Covers("trips/dispatch", "offer-sent-to-available-nearby-driver", Scope.Component, Quantification.Example)]
    [Covers("trips/dispatch", "unavailable-driver-not-offered", Scope.Component, Quantification.Example)]
    public async Task Only_available_nearby_drivers_are_offered()
    {
        var client = fixture.HttpClient.CreateClient();
        await fixture.Database.Save(
            Api.AvailableDriver("driver-0"),
            Api.AvailableDriver("driver-1"),
            Api.AvailableDriver("driver-2"),
            Api.UnavailableDriver("driver-3"),
            Api.UnavailableDriver("driver-4"),
            Api.AvailableDriver("driver-far", near: "uptown"));

        var trip = await client.RideId();

        var offered = (await Offers(client, trip)).Select(o => o.DriverId).ToHashSet();
        offered.Should().BeEquivalentTo("driver-0", "driver-1", "driver-2");
        offered.Should().NotContain("driver-3");
        offered.Should().NotContain("driver-4");
        offered.Should().NotContain("driver-far");
    }

    [Fact]
    [Covers("trips/dispatch", "no-available-drivers", Scope.Component, Quantification.Example)]
    public async Task No_available_drivers_means_no_offers()
    {
        var client = fixture.HttpClient.CreateClient();
        await fixture.Database.Save(
            Api.UnavailableDriver("driver-0"),
            Api.AvailableDriver("driver-far", near: "uptown"));

        var trip = await client.RideId();

        (await Offers(client, trip)).Should().BeEmpty();
        (await fixture.Database.Count<Offer>(o => true, Cancellation.Token())).Should().Be(0);
    }

    [Fact]
    [Covers("trips/dispatch", "expired-offer-withdrawn", Scope.Component, Quantification.Example)]
    public async Task An_offer_past_its_expiry_is_withdrawn()
    {
        var client = fixture.HttpClient.CreateClient();
        await fixture.Database.Save(Api.AvailableDriver("driver-0"), Api.AvailableDriver("driver-1"));
        var trip = await client.RideId();

        (await Offers(client, trip)).Should().OnlyContain(o => o.State == "offered");

        // Offers stand for thirty seconds; a minute later none of them do.
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        var after = await Offers(client, trip);
        after.Should().NotBeEmpty();
        after.Should().OnlyContain(o => o.State == "withdrawn");
    }
}
