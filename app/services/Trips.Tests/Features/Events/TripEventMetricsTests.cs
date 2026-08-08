using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Events;

[Collection(AdmissionTestsCollection.Name)]
public sealed class TripEventMetricsTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Metrics_expose_fresh_and_overdue_unpublished_events()
    {
        var client = fixture.HttpClient.CreateClient();
        await client.RideId();

        var fresh = await client.GetStringAsync("/operations/trip-events/metrics");
        fresh.Should().Contain("trips_event_outbox_pending 1");
        fresh.Should().Contain("trips_event_outbox_oldest_pending_age_seconds 0");

        fixture.Clock.Advance(TimeSpan.FromSeconds(45));
        var overdue = await client.GetStringAsync("/operations/trip-events/metrics");
        overdue.Should().Contain("trips_event_outbox_oldest_pending_age_seconds 45");
    }
}
