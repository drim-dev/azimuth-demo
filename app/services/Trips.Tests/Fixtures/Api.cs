using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Pricing;
using Trips.Domain;
using Trips.Features.Trips;
using Trips.Features.Referrals;

namespace Trips.Tests.Fixtures;

/// <summary>What a refusal looks like on the wire. The code is the half a client branches on.</summary>
public sealed record Problem(
    string? Title,
    int? Status,
    string? Detail,
    [property: JsonPropertyName("errorCode")] string? ErrorCode);

/// <summary>
/// The trip service's endpoints, named once.
/// </summary>
/// <remarks>
/// Tests speak these rather than raw paths so a route that moves is one edit. Every one of them
/// goes over HTTP: a component test that called a handler would pass against a slice whose endpoint
/// was never wired up.
/// </remarks>
public static class Api
{
    private static long _quoteId = 10_000;
    private static readonly QuoteTokenCodec Tokens = new("azimuth-demo-signing-key");

    public sealed record TestQuote(string Id, long TotalMinor, string Currency, DateTimeOffset ExpiresAt);

    /// <summary>Issues a quote and hands back its id, for the many tests that only need one.</summary>
    public static async Task<string> QuoteId(this HttpClient client) => (await client.Quote()).Id;

    /// <summary>
    /// Issues a quote and hands back the whole response, for tests that vary its terms and need the
    /// total and expiry the service actually decided rather than the ones they asked for.
    /// </summary>
    public static Task<TestQuote> Quote(
        this HttpClient client,
        long baseMinor = 1000,
        long distanceMinor = 500,
        string currency = "EUR")
    {
        _ = client;
        var id = Interlocked.Increment(ref _quoteId);
        var expiresAt = TripTestFixture.Start + TimeSpan.FromMinutes(2);
        QuoteComponent[] components = [new("base", baseMinor), new("distance", distanceMinor), new("surge", 0)];
        var total = checked(baseMinor + distanceMinor);
        var token = Tokens.Encode(new QuotePayload(
            id,
            "downtown",
            "airport",
            TripTestFixture.Start,
            expiresAt,
            "surge-v1",
            null,
            currency,
            components,
            total));
        return Task.FromResult(new TestQuote(token, total, currency.ToUpperInvariant(), expiresAt));
    }

    public static Task<HttpResponseMessage> RequestRide(
        this HttpClient client,
        string riderId,
        string quoteToken,
        string? referralCode = null,
        string? referralCreditId = null) =>
        client.PostAsJsonAsync(
            "/trips",
            new RequestRide.Request(riderId, quoteToken, referralCode, referralCreditId));

    public static async Task<GetReferralSummary.Response> ReferralSummary(
        this HttpClient client,
        string riderId) =>
        await (await client.PutAsync($"/referrals/{riderId}", null))
            .Read<GetReferralSummary.Response>();

    /// <summary>Requests a ride and hands back the trip id.</summary>
    public static async Task<string> RideId(this HttpClient client, string? riderId = null)
    {
        var response = await client.RequestRide(riderId ?? Rider(), await client.QuoteId());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Read<RequestRide.Response>()).TripId;
    }

    public static Task<HttpResponseMessage> GetTrip(this HttpClient client, string id) =>
        client.GetAsync($"/trips/{id}");

    public static Task<HttpResponseMessage> GetOffers(this HttpClient client, string id) =>
        client.GetAsync($"/trips/{id}/offers");

    public static Task<HttpResponseMessage> Accept(this HttpClient client, string id, string driverId) =>
        client.PostAsync($"/trips/{id}/accept/{driverId}", null);

    public static Task<HttpResponseMessage> Move(
        this HttpClient client,
        string id,
        string verb,
        string actor) =>
        client.PostAsync($"/trips/{id}/{verb}?actor={actor}", null);

    public static Task<HttpResponseMessage> GetOfferForDriver(
        this HttpClient client,
        string driverId,
        string id) =>
        client.GetAsync($"/drivers/{driverId}/offers/{id}");

    public static Task<HttpResponseMessage> GetTripForDriver(
        this HttpClient client,
        string driverId,
        string id) =>
        client.GetAsync($"/drivers/{driverId}/trips/{id}");

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    /// <summary>A rider nobody else in the run shares, so the active-trip index binds this test only.</summary>
    public static string Rider() => $"rider-{Guid.NewGuid():N}";

    public static Driver AvailableDriver(string id, string near = "downtown") => new()
    {
        Id = id,
        Available = true,
        Near = near,
        Display = "Sam",
        Vehicle = "blue hatchback",
        Position = "52.37,4.89",
    };

    public static Driver UnavailableDriver(string id, string near = "downtown") => new()
    {
        Id = id,
        Available = false,
        Near = near,
        Display = "Sam",
        Vehicle = "blue hatchback",
        Position = "52.37,4.89",
    };
}

public static class ProblemAssertions
{
    public static async Task ShouldBeRefused(
        this HttpResponseMessage response,
        HttpStatusCode status,
        string errorCode)
    {
        response.StatusCode.Should().Be(status);

        var problem = await response.Read<Problem>();
        problem.ErrorCode.Should().Be(errorCode);
        problem.Status.Should().Be((int)status);
        problem.Detail.Should().NotBeNullOrWhiteSpace();
    }

    public static Task ShouldBeBusinessRuleError(this HttpResponseMessage response, string errorCode) =>
        response.ShouldBeRefused(HttpStatusCode.UnprocessableEntity, errorCode);

    public static Task ShouldBeConflict(this HttpResponseMessage response, string errorCode) =>
        response.ShouldBeRefused(HttpStatusCode.Conflict, errorCode);

    public static Task ShouldBeNotFound(this HttpResponseMessage response, string errorCode) =>
        response.ShouldBeRefused(HttpStatusCode.NotFound, errorCode);
}
