using Azimuth.Annotations;
using Common.Messaging;
using Common.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Payments.Domain;
using Payments.Features.Events;
using Payments.Tests.Fixtures;
using Xunit;

namespace Payments.Tests.Features.Events;

[Collection(CaptureTestsCollection.Name)]
public sealed class PaymentEventRelayTests(PaymentsTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("payments/capture", "committed-capture-is-published", Scope.Component,
        Quantification.Universal, Oracle.Contract)]
    [Covers("payments/capture", "capture-publication-is-retryable", Scope.Component,
        Quantification.Universal, Oracle.Relational)]
    public async Task Relay_retry_reuses_the_committed_capture_fact()
    {
        var client = fixture.HttpClient.CreateClient();
        var relay = fixture.Service<PaymentEventRelay>();
        var random = new Random(20260810);

        for (var trial = 0; trial < 5; trial++)
        {
            await fixture.Reset(Cancellation.Token());
            var trip = 70_000 + trial;
            var credit = 80_000 + trial;
            var fare = random.NextInt64(1_000, 10_000);
            var creditMinor = random.NextInt64(1, fare + 1);
            await fixture.Database.Save(Api.CompletedTrip(
                trip,
                fare,
                referralCreditAuthority: Api.ReferralAuthority(credit, trip, creditMinor)));
            await client.Dispatch();

            var outbox = await fixture.Database.SingleOrDefault<PaymentEventOutbox>(
                item => item.TripId == trip,
                Cancellation.Token());
            outbox.Should().NotBeNull();
            var observed = new List<PaymentCaptured>();
            var relayAttempts = random.Next(2, 8);

            for (var attempt = 0; attempt < relayAttempts; attempt++)
            {
                await fixture.Database.Execute(async db =>
                    await db.PaymentEvents
                        .Where(item => item.EventId == outbox!.EventId)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(item => item.PublishedAt, (DateTimeOffset?)null),
                            Cancellation.Token()));

                (await relay.RelayPending(Cancellation.Token())).Should().Be(1);
                var body = await fixture.RabbitMq.Get(
                    PaymentEventTopology.ReferralsQueue,
                    Cancellation.Token());
                body.Should().NotBeNull();
                PaymentCapturedCodec.TryDeserialize(body!.Value.Span, out var message).Should().BeTrue();
                observed.Add(message!);
            }

            observed.Should().OnlyContain(message => message.EventId == outbox!.EventId);
            observed.Should().OnlyContain(message =>
                message.CaptureId == outbox!.CaptureId
                && message.TripId == trip
                && message.OriginalFareMinor == fare
                && message.ReferralCreditMinor == creditMinor
                && message.CapturedAmountMinor == fare - creditMinor
                && message.ReferralCreditId == credit);
            (await fixture.Database.Count<Capture>(
                item => item.TripId == trip,
                Cancellation.Token())).Should().Be(1);
            (await fixture.Database.Count<PaymentEventOutbox>(
                item => item.TripId == trip,
                Cancellation.Token())).Should().Be(1);
        }
    }
}
