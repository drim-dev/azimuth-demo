using System.Net;
using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Pricing.Service.Features.MarketPressure;
using Pricing.Service.Features.Quotes;
using Pricing.Tests.Fixtures;
using Xunit;

namespace Pricing.Tests.Features.Quotes;

[Collection(PricingTestsCollection.Name)]
public sealed class IssueQuoteTests(PricingTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("pricing/quote", "quote-returned", Scope.Component, Quantification.Example,
        Oracle.Contract)]
    [Covers("pricing/quote", "total-equals-components", Scope.Component, Quantification.Universal,
        Oracle.Relational)]
    [Covers("pricing/quote", "surge-is-a-quote-component", Scope.Component, Quantification.Universal,
        Oracle.Contract)]
    public async Task Every_serialized_quote_total_is_the_sum_of_its_three_components()
    {
        var client = fixture.HttpClient.CreateClient();
        var random = new Random(20260808);

        foreach (var currency in new[] { "EUR", "USD", "JPY" })
        {
            foreach (var pressure in new[] { (Open: 0, Available: 0), (Open: 7, Available: 2) })
            {
                await fixture.Reset(Cancellation.Token());
                (await client.ReportPressure(pressure.Open, pressure.Available)).StatusCode
                    .Should().Be(HttpStatusCode.OK);

                for (var trial = 0; trial < 16; trial++)
                {
                    var response = await client.IssueQuote(random.NextInt64(0, 5_000_000), currency);
                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    var quote = await response.Read<IssueQuote.Response>();

                    quote.Breakdown.Select(x => x.Label)
                        .Should().Equal("base", "distance", "surge");
                    quote.Id.Should().NotBeNullOrWhiteSpace();
                    quote.Token.Should().NotBeNullOrWhiteSpace();
                    quote.ExpiresAt.Should().BeAfter(PricingTestFixture.Start);
                    quote.Currency.Should().Be(currency);
                    quote.TotalMinor.Should().Be(quote.Breakdown.Sum(x => x.AmountMinor));
                    new QuoteTokenCodec("azimuth-demo-signing-key").Decode(quote.Token).TotalMinor
                        .Should().Be(quote.TotalMinor);
                }
            }
        }
    }

    [Fact]
    [Covers("pricing/quote", "quote-valid-before-expiry", Scope.Component, Quantification.Example)]
    [Covers("pricing/quote", "quote-invalid-after-expiry", Scope.Component, Quantification.Example)]
    [Covers("pricing/quote", "expired-quote-is-never-revalidated", Scope.Component,
        Quantification.Example)]
    public async Task A_quote_expires_without_changing_and_requoting_creates_a_new_identity()
    {
        var client = fixture.HttpClient.CreateClient();
        var issued = await (await client.IssueQuote()).Read<IssueQuote.Response>();

        var live = await (await client.GetAsync($"/quotes/{issued.Id}")).Read<GetQuote.Response>();
        live.Expired.Should().BeFalse();
        live.TotalMinor.Should().Be(issued.TotalMinor);

        fixture.Clock.Advance(issued.ExpiresAt - PricingTestFixture.Start - TimeSpan.FromTicks(1));
        (await (await client.GetAsync($"/quotes/{issued.Id}")).Read<GetQuote.Response>())
            .Expired.Should().BeFalse();

        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        var expired = await (await client.GetAsync($"/quotes/{issued.Id}")).Read<GetQuote.Response>();
        expired.Expired.Should().BeTrue();
        expired.TotalMinor.Should().Be(issued.TotalMinor);

        var reissued = await (await client.IssueQuote()).Read<IssueQuote.Response>();
        reissued.Id.Should().NotBe(issued.Id);
        (await (await client.GetAsync($"/quotes/{reissued.Id}")).Read<GetQuote.Response>())
            .Expired.Should().BeFalse();
        (await (await client.GetAsync($"/quotes/{issued.Id}")).Read<GetQuote.Response>())
            .Expired.Should().BeTrue();
    }

    [Fact]
    [Covers("pricing/quote", "current-pressure-selects-surge", Scope.Component,
        Quantification.Universal, Oracle.ModelBased)]
    public async Task Current_pressure_selects_the_integer_policy_for_all_relation_boundaries()
    {
        var client = fixture.HttpClient.CreateClient();
        (int Open, int Available)[] cases = [(0, 0), (1, 1), (2, 1), (100, 99), (0, 10)];

        foreach (var (open, available) in cases)
        {
            await fixture.Reset(Cancellation.Token());
            await client.ReportPressure(open, available);
            var quote = await (await client.IssueQuote(distanceMeters: 10_000)).Read<IssueQuote.Response>();
            var surge = quote.Breakdown.Single(x => x.Label == "surge").AmountMinor;
            var independentlyModelled = open > available ? (500 + 1_000) / 5 : 0;
            surge.Should().Be(independentlyModelled);
        }
    }

    [Fact]
    [Covers("pricing/quote", "stale-pressure-does-not-select-surge", Scope.Component,
        Quantification.Universal)]
    public async Task Pressure_is_current_only_on_the_near_side_of_the_freshness_boundary()
    {
        var client = fixture.HttpClient.CreateClient();
        await client.ReportPressure(openRequests: 2, availableDrivers: 1);

        fixture.Clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        var current = await (await client.IssueQuote()).Read<IssueQuote.Response>();
        current.Breakdown.Single(x => x.Label == "surge").AmountMinor.Should().BePositive();

        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        var stale = await (await client.IssueQuote()).Read<IssueQuote.Response>();
        stale.Breakdown.Single(x => x.Label == "surge").AmountMinor.Should().Be(0);
    }

    public sealed class ValidatorTests
    {
        private readonly IssueQuote.RequestValidator _quote = new();
        private readonly ReportMarketPressure.RequestValidator _pressure = new();

        [Fact]
        public void A_quote_requires_a_pickup_dropoff_currency_and_nonnegative_distance()
        {
            var result = _quote.TestValidate(new IssueQuote.Request("outside", "", -1, "EU"));
            result.ShouldHaveValidationErrorFor(x => x.Pickup);
            result.ShouldHaveValidationErrorFor(x => x.Dropoff);
            result.ShouldHaveValidationErrorFor(x => x.DistanceMeters);
            result.ShouldHaveValidationErrorFor(x => x.Currency);
        }

        [Fact]
        public void Pressure_counts_cannot_be_negative()
        {
            var result = _pressure.TestValidate(new ReportMarketPressure.Request("", -1, -1));
            result.ShouldHaveValidationErrorFor(x => x.Market);
            result.ShouldHaveValidationErrorFor(x => x.OpenRequests);
            result.ShouldHaveValidationErrorFor(x => x.AvailableDrivers);
        }
    }
}
