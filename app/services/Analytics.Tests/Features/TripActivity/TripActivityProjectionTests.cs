using System.Net;
using System.Net.Http.Json;
using Analytics.Database;
using Analytics.Domain;
using Analytics.Features.TripActivity;
using Analytics.Tests.Fixtures;
using Azimuth.Annotations;
using Common.Messaging;
using Common.Identity;
using Common.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Analytics.Tests.Features.TripActivity;

[Collection(TripActivityTestsCollection.Name)]
public sealed class TripActivityProjectionTests(AnalyticsTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("analytics/trip-activity", "redelivery-is-counted-once", Scope.Component,
        Quantification.Universal)]
    [Covers("analytics/trip-activity", "older-delivery-is-inert", Scope.Component,
        Quantification.Universal, Oracle.ModelBased)]
    public async Task Projection_follows_the_maximum_version_across_reordering_and_redelivery()
    {
        var random = new Random(8217);
        for (var trial = 0; trial < 8; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            var tripId = 10_000 + trial;
            var history = new[]
            {
                Event(tripId, 1, "requested"),
                Event(tripId, 2, "assigned"),
                Event(tripId, 3, "in-progress"),
                Event(tripId, 4, "completed"),
            };
            var deliveries = history
                .SelectMany(message => Enumerable.Repeat(message, random.Next(1, 9)))
                .OrderBy(_ => random.Next())
                .ToArray();

            foreach (var message in deliveries)
            {
                await fixture.RabbitMq.Publish(message, Cancellation.Token());
            }

            var expected = deliveries.MaxBy(x => x.Version)!;
            var projected = await WaitForProjection(tripId, expected.Version);
            projected.State.Should().Be(expected.State);
            (await Summary()).TotalTrips.Should().Be(1);
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (await fixture.Database.Count<TripEventInbox>(
                    x => x.TripId == tripId,
                    Cancellation.Token()) == history.Length)
                {
                    break;
                }

                await Task.Delay(25);
            }
            (await fixture.Database.Count<TripEventInbox>(
                x => x.TripId == tripId,
                Cancellation.Token())).Should().Be(history.Length);
        }
    }

    [Fact]
    [Covers("analytics/trip-activity", "malformed-event-is-dead-lettered", Scope.Component,
        Quantification.Example)]
    public async Task Malformed_delivery_is_dead_lettered_without_blocking_the_valid_one()
    {
        var malformed = TripStateChangedCodec.Serialize(Event(20_001, 1, "teleported"));
        await fixture.RabbitMq.PublishMalformed(malformed, Cancellation.Token());
        var valid = Event(20_000, 1, "requested");
        await fixture.RabbitMq.Publish(valid, Cancellation.Token());

        (await WaitForProjection(valid.TripId, valid.Version)).State.Should().Be(valid.State);

        ReadOnlyMemory<byte>? deadLetter = null;
        for (var attempt = 0; attempt < 100 && deadLetter is null; attempt++)
        {
            deadLetter = await fixture.RabbitMq.Get(
                TripEventTopology.AnalyticsDeadLetterQueue,
                Cancellation.Token());
            if (deadLetter is null)
            {
                await Task.Delay(25);
            }
        }

        deadLetter.Should().NotBeNull();
        deadLetter!.Value.ToArray().Should().Equal(malformed);
    }

    private async Task<GetTripActivity.Response> WaitForProjection(long tripId, long version)
    {
        var client = fixture.HttpClient.CreateClient();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var response = await client.GetAsync($"/activity/trips/{IdEncoding.Encode(tripId)}");
            if (response.StatusCode == HttpStatusCode.OK
                && await response.Content.ReadFromJsonAsync<GetTripActivity.Response>() is { } activity
                && activity.Version == version)
            {
                return activity;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"trip {tripId} did not reach activity version {version}");
    }

    private async Task<GetTripActivitySummary.Response> Summary() =>
        (await fixture.HttpClient.CreateClient()
            .GetFromJsonAsync<GetTripActivitySummary.Response>("/activity/summary"))!;

    private static TripStateChanged Event(long tripId, long version, string state) => new(
        Guid.NewGuid(),
        tripId,
        version,
        state,
        AnalyticsTestFixture.Start.AddMinutes(version),
        "signed-quote",
        "default");
}
