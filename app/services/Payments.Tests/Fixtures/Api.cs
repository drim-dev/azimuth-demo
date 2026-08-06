using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Common.Identity;
using FluentAssertions;
using Payments.Domain;
using Payments.Features.Captures;

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
    public static Task<HttpResponseMessage> Dispatch(this HttpClient client, string? adjustmentReason = null) =>
        client.PostAsync(
            adjustmentReason is null ? "/dispatch" : $"/dispatch?adjustmentReason={adjustmentReason}",
            null);

    public static Task<HttpResponseMessage> GetCapture(this HttpClient client, long tripId) =>
        client.GetAsync($"/captures/{IdEncoding.Encode(tripId)}");

    public static Task<HttpResponseMessage> GetFailures(this HttpClient client, long tripId) =>
        client.GetAsync($"/captures/{IdEncoding.Encode(tripId)}/failures");

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
            AmountMinor = amountMinor,
            Currency = currency,
            WrittenAt = PaymentsTestFixture.Start,
        };
}
