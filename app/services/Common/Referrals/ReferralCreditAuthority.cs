using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azimuth.Annotations;

namespace Common.Referrals;

public sealed record ReferralCreditAuthority(
    long CreditId,
    long TripId,
    long AmountMinor,
    string Currency);

public sealed class InvalidReferralCreditAuthorityException(string message) : Exception(message);

/// <summary>Authenticates the exact credit reservation that Payments may apply to one trip.</summary>
public sealed class ReferralCreditAuthorityCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _key;

    public ReferralCreditAuthorityCodec(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("a referral credit signing key is required", nameof(key));
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    [ImplementsMechanism("referrals/rewards", "referral-credit-authority-issuance")]
    public string Encode(ReferralCreditAuthority authority)
    {
        Validate(authority);
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(authority, JsonOptions));
        var signature = Base64Url(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(body)));
        return $"{body}.{signature}";
    }

    [ImplementsMechanism("referrals/rewards", "referral-credit-authority-validation")]
    public ReferralCreditAuthority Decode(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            throw Invalid("the referral credit authority is malformed");
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
            throw Invalid("the referral credit authority is malformed");
        }

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw Invalid("the referral credit authority signature is invalid");
        }

        try
        {
            var authority = JsonSerializer.Deserialize<ReferralCreditAuthority>(
                    FromBase64Url(parts[0]), JsonOptions)
                ?? throw Invalid("the referral credit authority has no payload");
            Validate(authority);
            return authority;
        }
        catch (JsonException)
        {
            throw Invalid("the referral credit authority payload is malformed");
        }
        catch (FormatException)
        {
            throw Invalid("the referral credit authority payload is malformed");
        }
    }

    private static void Validate(ReferralCreditAuthority authority)
    {
        if (authority.CreditId <= 0
            || authority.TripId <= 0
            || authority.AmountMinor <= 0
            || string.IsNullOrWhiteSpace(authority.Currency))
        {
            throw Invalid("the referral credit authority omits required values");
        }
    }

    private static InvalidReferralCreditAuthorityException Invalid(string message) => new(message);

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
