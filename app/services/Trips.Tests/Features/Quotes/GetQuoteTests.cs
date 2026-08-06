using System.Net;
using Azimuth.Annotations;
using Common.Testing;
using FluentAssertions;
using Trips.Features.Quotes;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Quotes;

[Collection(QuotesTestsCollection.Name)]
public sealed class GetQuoteTests(TripTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Covers("pricing/quote", "quote-valid-before-expiry", Scope.Component, Quantification.Invariant)]
    [Covers("pricing/quote", "quote-invalid-after-expiry", Scope.Component, Quantification.Invariant)]
    public async Task A_quote_is_valid_until_its_expiry_and_not_after()
    {
        var client = fixture.HttpClient.CreateClient();
        var id = await client.QuoteId();

        var beforeExpiry = await (await client.GetQuote(id)).Read<GetQuote.Response>();
        beforeExpiry.Expired.Should().BeFalse();
        beforeExpiry.TotalMinor.Should().Be(1500);

        // The same quote, one second past the two minutes it was issued for.
        fixture.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

        var afterExpiry = await (await client.GetQuote(id)).Read<GetQuote.Response>();
        afterExpiry.Expired.Should().BeTrue();
        afterExpiry.TotalMinor.Should().Be(1500);
    }

    /// <summary>
    /// Expiry is derived on read rather than written by a sweeper, so there is no path that moves
    /// it back. Asked again after more time has passed, an expired quote is still expired.
    /// </summary>
    [Fact]
    [Covers("pricing/quote", "expired-quote-is-never-revalidated", Scope.Component, Quantification.Invariant)]
    public async Task An_expired_quote_stays_expired_and_a_new_one_gets_a_new_identity()
    {
        var client = fixture.HttpClient.CreateClient();
        var expired = await client.QuoteId();

        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        (await (await client.GetQuote(expired)).Read<GetQuote.Response>()).Expired.Should().BeTrue();

        var reissued = await client.QuoteId();
        reissued.Should().NotBe(expired);
        (await (await client.GetQuote(reissued)).Read<GetQuote.Response>()).Expired.Should().BeFalse();

        // And the old one has not been revived by the new one existing.
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        (await (await client.GetQuote(expired)).Read<GetQuote.Response>()).Expired.Should().BeTrue();
    }

    [Fact]
    [Untraced("absence of a resource; no claim asserts the shape of a miss")]
    public async Task An_unknown_quote_is_absent()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.GetQuote("0000000000000");

        await response.ShouldBeNotFound("pricing:quote:get:not_found");
    }

    /// <summary>An id from a URL is untrusted input: a malformed one is absence, not a fault.</summary>
    [Fact]
    [Untraced("guards the decode boundary; no claim asserts it")]
    public async Task A_malformed_quote_id_is_absent_rather_than_an_error()
    {
        var client = fixture.HttpClient.CreateClient();

        var response = await client.GetQuote("not-a-real-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
