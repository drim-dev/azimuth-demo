using System.Net;
using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Trips.Domain;
using Trips.Features.Quotes;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Quotes;

[Collection(QuotesTestsCollection.Name)]
public sealed class IssueQuoteTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("pricing/quote", "quote-returned", Scope.Component, Quantification.Universal)]
    public async Task An_issued_quote_carries_a_total_a_currency_and_an_expiry()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.IssueQuote(baseMinor: 1000, distanceMinor: 500, currency: "EUR");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await response.Read<IssueQuote.Response>();
        quote.Id.Should().NotBeNullOrWhiteSpace();
        quote.TotalMinor.Should().Be(1500);
        quote.Currency.Should().Be("EUR");
        quote.ExpiresAt.Should().Be(TripTestFixture.Start + TimeSpan.FromMinutes(2));

        var stored = await fixture.Database.SingleOrDefault<Quote>(
            q => q.TotalMinor == 1500, Cancellation.Token());
        stored.Should().NotBeNull();
        stored!.Currency.Should().Be("EUR");
    }

    /// <summary>
    /// The breakdown is part of what a quote is, not a rendering choice: a rider who cannot see
    /// what they are being charged for cannot dispute it.
    /// </summary>
    [Fact]
    [Covers("pricing/quote", "breakdown-accompanies-quote", Scope.Component, Quantification.Universal)]
    public async Task An_issued_quote_carries_the_components_that_make_up_its_total()
    {
        var client = fixture.HttpClient.CreateClient();

        var quote = await (await client.IssueQuote(baseMinor: 1000, distanceMinor: 500))
            .Read<IssueQuote.Response>();

        quote.Breakdown.Select(c => c.Label).Should().Equal("base", "distance");
        quote.Breakdown.Sum(c => c.AmountMinor).Should().Be(quote.TotalMinor);
    }

    /// <summary>
    /// The rule's own tests, in the validator rather than through the endpoint.
    /// </summary>
    /// <remarks>
    /// Left untraced deliberately. <c>unserviceable-area</c> is planned at <c>e2e</c>, where the
    /// refusal is observable to a rider, and a <c>Covers</c> here would claim the claim at a scope
    /// the plan does not accept — which is the <c>wrong-form</c> finding the trip service's unit
    /// tests carried before the e2e rung existed.
    /// </remarks>
    public sealed class ValidatorTests
    {
        private readonly IssueQuote.RequestValidator _validator = new();

        private static IssueQuote.Request Request(string pickup = "a", string currency = "EUR") =>
            new(pickup, "b", 1000, 500, currency);

        [Fact]
        [Untraced("the rule's claim is planned at e2e scope; this guards the rule, not the claim")]
        public void A_journey_with_no_pickup_is_refused_as_unserviceable()
        {
            _validator.TestValidate(Request(pickup: string.Empty))
                .ShouldHaveValidationErrorFor(x => x.Pickup)
                .WithErrorCode("pricing:quote:issue:unserviceable_area");
        }

        [Fact]
        [Untraced("the rule's claim is planned at e2e scope; this guards the rule, not the claim")]
        public void A_journey_with_a_blank_pickup_is_refused_as_unserviceable()
        {
            _validator.TestValidate(Request(pickup: "   "))
                .ShouldHaveValidationErrorFor(x => x.Pickup);
        }

        [Fact]
        [Untraced("shape only; no claim asserts that a quote states a currency at this scope")]
        public void A_quote_states_a_currency()
        {
            _validator.TestValidate(Request(currency: string.Empty))
                .ShouldHaveValidationErrorFor(x => x.Currency);
        }

        [Fact]
        [Untraced("the accepting half of the rules above")]
        public void A_serviceable_journey_passes()
        {
            _validator.TestValidate(Request()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
