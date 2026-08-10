using Common.Messaging;
using FluentAssertions;
using Xunit;

namespace Pricing.Tests;

public sealed class PaymentCapturedContractTests
{
    [Fact]
    public void Capture_fact_round_trips_its_auditable_breakdown()
    {
        var message = new PaymentCaptured(
            Guid.NewGuid(),
            CaptureId: 11,
            TripId: 17,
            OriginalFareMinor: 1_700,
            ReferralCreditMinor: 500,
            CapturedAmountMinor: 1_200,
            Currency: "USD",
            ReferralCreditId: 23,
            OccurredAt: DateTimeOffset.Parse("2026-08-10T12:00:00Z"));

        PaymentCapturedCodec.TryDeserialize(
            PaymentCapturedCodec.Serialize(message), out var decoded).Should().BeTrue();
        decoded.Should().Be(message);
    }

    [Fact]
    public void Inconsistent_breakdown_is_rejected()
    {
        var message = new PaymentCaptured(
            Guid.NewGuid(),
            CaptureId: 11,
            TripId: 17,
            OriginalFareMinor: 1_700,
            ReferralCreditMinor: 500,
            CapturedAmountMinor: 1_300,
            Currency: "USD",
            ReferralCreditId: 23,
            OccurredAt: DateTimeOffset.Parse("2026-08-10T12:00:00Z"));

        PaymentCapturedCodec.TryDeserialize(
            PaymentCapturedCodec.Serialize(message), out _).Should().BeFalse();
    }
}
