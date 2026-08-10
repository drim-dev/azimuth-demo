using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azimuth.Annotations;

namespace Pricing;

public sealed record QuoteComponent(string Label, long AmountMinor);

public sealed record QuotePayload(
    long QuoteId,
    string Pickup,
    string Dropoff,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string Policy,
    long? PressureId,
    string Currency,
    IReadOnlyList<QuoteComponent> Components,
    long TotalMinor);

public sealed class InvalidQuoteTokenException(string message) : Exception(message);

/// <summary>Authenticates an immutable quote and rejects internally inconsistent payloads.</summary>
public sealed class QuoteTokenCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _key;

    public QuoteTokenCodec(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("a quote signing key is required", nameof(key));
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    [ImplementsMechanism("security/quote-tokens", "quote-token-issuance")]
    [Realizes("security/quote-tokens", "issued-token-round-trips")]
    public string Encode(QuotePayload payload)
    {
        Validate(payload);
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signature = Base64Url(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(body)));
        return $"{body}.{signature}";
    }

    [ImplementsMechanism("security/quote-tokens", "quote-token-validation")]
    [Realizes("security/quote-tokens", "issued-token-round-trips")]
    [Realizes("security/quote-tokens", "altered-token-rejected")]
    [Realizes("security/quote-tokens", "foreign-signature-rejected")]
    public QuotePayload Decode(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            throw Invalid("the quote token is malformed");
        }

        byte[] actual;
        byte[] expected;
        try
        {
            actual = FromBase64Url(parts[1]);
            expected = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(parts[0]));
        }
        catch (FormatException)
        {
            throw Invalid("the quote token is malformed");
        }

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw Invalid("the quote token signature is invalid");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<QuotePayload>(FromBase64Url(parts[0]), JsonOptions)
                ?? throw Invalid("the quote token has no payload");
            Validate(payload);
            return payload;
        }
        catch (JsonException)
        {
            throw Invalid("the quote token payload is malformed");
        }
        catch (FormatException)
        {
            throw Invalid("the quote token payload is malformed");
        }
    }

    private static void Validate(QuotePayload payload)
    {
        if (payload.QuoteId <= 0 || string.IsNullOrWhiteSpace(payload.Currency))
        {
            throw Invalid("the quote token omits required identity or currency");
        }

        if (payload.ExpiresAt <= payload.IssuedAt)
        {
            throw Invalid("the quote token has an invalid lifetime");
        }

        if (payload.Components.Count == 0
            || payload.Components.Any(c => string.IsNullOrWhiteSpace(c.Label) || c.AmountMinor < 0)
            || payload.Components.Select(c => c.Label).Distinct(StringComparer.Ordinal).Count()
                != payload.Components.Count)
        {
            throw Invalid("the quote token has invalid components");
        }

        long sum;
        try
        {
            sum = payload.Components.Aggregate(0L, (total, component) => checked(total + component.AmountMinor));
        }
        catch (OverflowException)
        {
            throw Invalid("the quote component sum overflows minor units");
        }

        if (sum != payload.TotalMinor)
        {
            throw Invalid("the quote total does not equal its components");
        }
    }

    private static InvalidQuoteTokenException Invalid(string message) => new(message);

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => "",
            _ => throw new FormatException(),
        };
        var decoded = Convert.FromBase64String(padded);
        if (!string.Equals(Base64Url(decoded), value, StringComparison.Ordinal))
        {
            throw new FormatException();
        }
        return decoded;
    }
}
