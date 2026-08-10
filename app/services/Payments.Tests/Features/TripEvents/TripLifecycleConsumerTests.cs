using Azimuth.Annotations;
using Common.Messaging;
using Common.Testing;
using FluentAssertions;
using Payments.Domain;
using Payments.Tests.Fixtures;
using Xunit;

namespace Payments.Tests.Features.TripEvents;

[Collection(CaptureTestsCollection.Name)]
public sealed class TripLifecycleConsumerTests(PaymentsTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("payments/capture", "duplicate-completion-event", Scope.Component,
        Quantification.Universal)]
    public async Task Redelivery_and_older_versions_create_one_settlement_intent()
    {
        var completed = Event(
            version: 4,
            state: "completed",
            referralCreditAuthority: "signed-referral-credit");
        for (var delivery = 0; delivery < 7; delivery++)
        {
            await fixture.RabbitMq.Publish(completed, Cancellation.Token());
        }

        await fixture.RabbitMq.Publish(Event(version: 2, state: "assigned"), Cancellation.Token());

        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await fixture.Database.Count<CaptureIntent>(
                    x => x.TripId == completed.TripId,
                    Cancellation.Token()) == 1
                && await fixture.Database.Count<TripEventInbox>(
                    x => x.TripId == completed.TripId,
                    Cancellation.Token()) == 2)
            {
                break;
            }

            await Task.Delay(25);
        }

        var intent = await fixture.Database.SingleOrDefault<CaptureIntent>(
            x => x.TripId == completed.TripId,
            Cancellation.Token());
        intent.Should().NotBeNull();
        intent!.ReferralCreditAuthority.Should().Be(completed.ReferralCreditAuthority);
        (await fixture.Database.SingleOrDefault<TripEventCursor>(
            x => x.TripId == completed.TripId,
            Cancellation.Token()))!.Version.Should().Be(completed.Version);
    }

    private static TripStateChanged Event(
        long version,
        string state,
        string? referralCreditAuthority = null) => new(
        Guid.NewGuid(),
        30_000,
        version,
        state,
        PaymentsTestFixture.Start.AddMinutes(version),
        "signed-quote",
        "default",
        referralCreditAuthority);
}
