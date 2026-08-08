using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Common.Identity;
using FluentAssertions;
using Payments.Domain;
using Payments.Features.Captures;
using Pricing;

namespace Payments.Tests.Fixtures;

public sealed record Problem(
    int? Status,
    string? Detail,
    [property: JsonPropertyName("errorCode")] string? ErrorCode);

/// <summary>
/// The payments service's endpoints, named once.
/// </summary>
/// <remarks>
/// The outbox row is seeded rather than posted, because that is how it arrives in production: the
/// trip service writes it in the same transaction as the completion, and payments is only ever its
/// reader. Seeding it is the faithful arrangement, not a shortcut around a missing endpoint.
/// </remarks>
public static class Api
{
    private static long _quoteId = 50_000;
    private static readonly QuoteTokenCodec Tokens = new("azimuth-demo-signing-key");
    public static Task<HttpResponseMessage> Dispatch(
        this HttpClient client,
        string? adjustmentReason = null,
        long adjustmentMinor = 0) =>
        client.PostAsync(
            adjustmentReason is null
                ? "/dispatch"
                : $"/dispatch?adjustmentReason={adjustmentReason}&adjustmentMinor={adjustmentMinor}",
            null);

    public static Task<HttpResponseMessage> GetCapture(this HttpClient client, long tripId) =>
        client.GetAsync($"/captures/{IdEncoding.Encode(tripId)}");

    public static Task<HttpResponseMessage> GetFailures(this HttpClient client, long tripId) =>
        client.GetAsync($"/captures/{IdEncoding.Encode(tripId)}/failures");

    public static Task<HttpResponseMessage> GetStatus(this HttpClient client, long tripId) =>
        client.GetAsync($"/captures/{IdEncoding.Encode(tripId)}/status");

    public static Task<HttpResponseMessage> GetSettlementMetrics(this HttpClient client) =>
        client.GetAsync("/operations/metrics");

    public static Task<HttpResponseMessage> UpdatePaymentMethod(
        this HttpClient client,
        long tripId,
        string paymentMethod) =>
        client.PutAsJsonAsync(
            $"/captures/{IdEncoding.Encode(tripId)}/payment-method",
            new { paymentMethod });

    /// <summary>The capture a trip has, or null when it has none.</summary>
    public static async Task<GetCapture.Response?> Capture(this HttpClient client, long tripId)
    {
        var response = await client.GetCapture(tripId);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Read<GetCapture.Response>();
    }

    public static async Task<IReadOnlyList<string>> Failures(this HttpClient client, long tripId) =>
        (await (await client.GetFailures(tripId)).Read<IReadOnlyList<string>>());

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    /// <summary>What the trip service writes when a trip completes.</summary>
    public static CaptureIntent CompletedTrip(long tripId, long amountMinor, string currency = "EUR") =>
        new()
        {
            TripId = tripId,
            QuoteToken = Quote(amountMinor, currency),
            PaymentMethod = "default",
            WrittenAt = PaymentsTestFixture.Start,
        };

    public static string Quote(long amountMinor, string currency = "EUR")
    {
        var id = Interlocked.Increment(ref _quoteId);
        var baseMinor = amountMinor / 3;
        QuoteComponent[] components =
        [
            new("base", baseMinor),
            new("distance", amountMinor - baseMinor),
            new("surge", 0),
        ];
        return Tokens.Encode(new QuotePayload(
            id,
            "downtown",
            "airport",
            PaymentsTestFixture.Start,
            PaymentsTestFixture.Start + TimeSpan.FromMinutes(2),
            "surge-v1",
            null,
            currency,
            components,
            amountMinor));
    }
}
