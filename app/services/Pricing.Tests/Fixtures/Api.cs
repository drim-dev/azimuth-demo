using System.Net.Http.Json;
using Pricing.Service.Features.MarketPressure;
using Pricing.Service.Features.Quotes;

namespace Pricing.Tests.Fixtures;

public static class Api
{
    public static Task<HttpResponseMessage> ReportPressure(
        this HttpClient client,
        int openRequests,
        int availableDrivers,
        string market = "downtown") =>
        client.PostAsJsonAsync(
            "/market-pressure",
            new ReportMarketPressure.Request(market, openRequests, availableDrivers));

    public static Task<HttpResponseMessage> IssueQuote(
        this HttpClient client,
        long distanceMeters = 10_000,
        string currency = "EUR",
        string pickup = "downtown") =>
        client.PostAsJsonAsync(
            "/quotes",
            new IssueQuote.Request(pickup, "airport", distanceMeters, currency));

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;
}
