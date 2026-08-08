using System.Net;
using Common.Testing;
using FluentAssertions;
using Payments.Domain;
using Payments.Features.Captures;
using Payments.Tests.Fixtures;
using Xunit;

namespace Payments.Tests.Features.Captures;

[Collection(CaptureTestsCollection.Name)]
public sealed class PaymentStatusTests(PaymentsTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Status_names_each_payment_state_without_encoding_it_as_color()
    {
        var client = fixture.HttpClient.CreateClient();
        await fixture.Database.Save(Api.CompletedTrip(1000, 1500));

        var pending = await (await client.GetStatus(1000)).Read<GetPaymentStatus.Response>();
        pending.Status.Should().Be("pending");
        pending.Message.Should().Contain("No action");

        fixture.Provider.Script(ProviderOutcome.Declined);
        await client.Dispatch();
        var declined = await (await client.GetStatus(1000)).Read<GetPaymentStatus.Response>();
        declined.Status.Should().Be("declined");
        declined.Message.Should().Contain("retried");

        fixture.Provider.Script(ProviderOutcome.Captured);
        await client.UpdatePaymentMethod(1000, "replacement-token");
        await client.Dispatch();
        var captured = await (await client.GetStatus(1000)).Read<GetPaymentStatus.Response>();
        captured.Status.Should().Be("captured");
        captured.AmountMinor.Should().Be(1500);
        captured.Currency.Should().Be("EUR");

        var none = await (await client.GetStatus(2000)).Read<GetPaymentStatus.Response>();
        none.Status.Should().Be("none");
        none.Message.Should().Contain("No payment");
    }

    [Fact]
    public async Task Settlement_metrics_distinguish_fresh_and_overdue_intents()
    {
        var client = fixture.HttpClient.CreateClient();
        await fixture.Database.Save(
            Api.CompletedTrip(1000, 1500),
            new CaptureIntent
            {
                TripId = 2000,
                QuoteToken = Api.Quote(2300),
                PaymentMethod = "default",
                WrittenAt = PaymentsTestFixture.Start - TimeSpan.FromMinutes(3),
            });

        var response = await client.GetSettlementMetrics();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metrics = await response.Content.ReadAsStringAsync();

        metrics.Should().Contain("payments_capture_pending_intents 2");
        metrics.Should().Contain("payments_capture_overdue_intents 1");
        metrics.Should().Contain("payments_capture_oldest_pending_age_seconds 180");
    }
}
