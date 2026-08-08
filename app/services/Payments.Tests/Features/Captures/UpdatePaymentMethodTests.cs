using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Payments.Domain;
using Payments.Features.Captures;
using Payments.Tests.Fixtures;
using Xunit;

namespace Payments.Tests.Features.Captures;

[Collection(CaptureTestsCollection.Name)]
public sealed class UpdatePaymentMethodTests(PaymentsTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("payments/capture", "declined-capture-is-retryable", Scope.Component, Quantification.Example)]
    public async Task A_declined_capture_retries_with_the_replacement_payment_method()
    {
        var client = fixture.HttpClient.CreateClient();
        fixture.Provider.Script(ProviderOutcome.Declined, ProviderOutcome.Captured);
        await fixture.Database.Save(Api.CompletedTrip(1000, 1500));

        await client.Dispatch();
        (await client.Capture(1000)).Should().BeNull();
        (await client.Failures(1000)).Should().ContainSingle();
        await client.Dispatch();
        fixture.Provider.Calls.Should().Be(1);

        await client.UpdatePaymentMethod(1000, "replacement-token");
        (await client.Failures(1000)).Should().BeEmpty();
        (await (await client.GetStatus(1000)).Read<GetPaymentStatus.Response>())
            .Status.Should().Be("pending");

        await client.Dispatch();

        (await client.Capture(1000)).Should().NotBeNull();
        fixture.Provider.PaymentMethods.Should().Equal("default", "replacement-token");
    }

    public sealed class ValidatorTests
    {
        private readonly UpdatePaymentMethod.RequestValidator _validator = new();

        [Fact]
        public void Payment_method_is_required()
        {
            var result = _validator.TestValidate(new UpdatePaymentMethod.Request("trip", ""));
            result.ShouldHaveValidationErrorFor(request => request.PaymentMethod);
        }

        [Fact]
        public void Payment_method_has_a_bounded_transport_shape()
        {
            var result = _validator.TestValidate(
                new UpdatePaymentMethod.Request("trip", new string('x', 129)));
            result.ShouldHaveValidationErrorFor(request => request.PaymentMethod);
        }

        [Fact]
        public void Opaque_payment_method_tokens_are_accepted()
        {
            var result = _validator.TestValidate(
                new UpdatePaymentMethod.Request("trip", "replacement-token"));
            result.ShouldNotHaveValidationErrorFor(request => request.PaymentMethod);
        }
    }
}
