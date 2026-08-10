using Azimuth.Annotations;
using FluentAssertions;
using Xunit;

namespace Pricing.Tests;

public sealed class QuoteTokenTests
{
    private const string Base64UrlAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [Covers("security/quote-tokens", "issued-token-round-trips", Scope.Unit,
        Quantification.Universal, Oracle.Direct)]
    [CoversMechanism("security/quote-tokens", "quote-token-issuance", Scope.Unit,
        Quantification.Universal, Oracle.Direct)]
    [CoversMechanism("security/quote-tokens", "quote-token-validation", Scope.Unit,
        Quantification.Universal, Oracle.Direct)]
    [Fact]
    public void Issued_tokens_round_trip_generated_payloads()
    {
        var codec = new QuoteTokenCodec("test-key");

        foreach (var payload in Payloads(64, 20260810))
        {
            codec.Decode(codec.Encode(payload))
                .Should().BeEquivalentTo(payload, options => options.WithStrictOrdering());
        }
    }

    [Covers("security/quote-tokens", "altered-token-rejected", Scope.Unit,
        Quantification.Universal, Oracle.Metamorphic)]
    [CoversMechanism("security/quote-tokens", "quote-token-validation", Scope.Unit,
        Quantification.Universal, Oracle.Metamorphic)]
    [Fact]
    public void Every_encoded_position_participates_in_authentication()
    {
        var codec = new QuoteTokenCodec("test-key");

        foreach (var payload in Payloads(8, 20260811))
        {
            var token = codec.Encode(payload);
            codec.Decode(token)
                .Should().BeEquivalentTo(payload, options => options.WithStrictOrdering());

            for (var index = 0; index < token.Length; index++)
            {
                if (token[index] == '.') continue;
                var replacement = token[index] == 'A' ? 'B' : 'A';
                AssertRejected(codec, token, index, replacement);
            }

            var finalSignaturePosition = token.Length - 1;
            foreach (var replacement in Base64UrlAlphabet)
            {
                if (replacement == token[finalSignaturePosition]) continue;
                AssertRejected(codec, token, finalSignaturePosition, replacement);
            }
        }
    }

    [Covers("security/quote-tokens", "foreign-signature-rejected", Scope.Unit,
        Quantification.Universal, Oracle.Metamorphic)]
    [CoversMechanism("security/quote-tokens", "quote-token-validation", Scope.Unit,
        Quantification.Universal, Oracle.Metamorphic)]
    [Fact]
    public void Independently_generated_authorities_reject_each_others_tokens()
    {
        var random = new Random(20260812);
        foreach (var payload in Payloads(64, 20260813))
        {
            var issuer = new QuoteTokenCodec($"issuer-{Key(random)}");
            var otherAuthority = new QuoteTokenCodec($"other-{Key(random)}");
            var token = issuer.Encode(payload);

            issuer.Decode(token)
                .Should().BeEquivalentTo(payload, options => options.WithStrictOrdering());
            var decodeByOtherAuthority = () => otherAuthority.Decode(token);
            decodeByOtherAuthority.Should().Throw<InvalidQuoteTokenException>();
        }
    }

    private static IEnumerable<QuotePayload> Payloads(int count, int seed)
    {
        yield return new QuotePayload(
            1,
            "p",
            "d",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddTicks(1),
            "p",
            null,
            "A",
            [new("c", 0)],
            0);
        yield return new QuotePayload(
            long.MaxValue,
            "pickup-\u0000-\ud83d\ude95",
            "dropoff-\n",
            DateTimeOffset.MaxValue.AddTicks(-1),
            DateTimeOffset.MaxValue,
            "policy-\u03bb",
            long.MaxValue,
            "ZZZ",
            [new("maximum", long.MaxValue)],
            long.MaxValue);
        yield return new QuotePayload(
            long.MaxValue - 1,
            "pickup",
            "dropoff",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(1),
            "policy",
            1,
            "XYZ",
            [new("large", long.MaxValue - 1), new("remainder", 1)],
            long.MaxValue);

        var random = new Random(seed);
        for (var trial = 0; trial < count; trial++)
        {
            var components = Enumerable.Range(0, random.Next(1, 7))
                .Select(index => new QuoteComponent(
                    $"component-{index}-{random.NextInt64()}",
                    random.NextInt64(0, 1_000_000)))
                .ToArray();
            var issuedAt = DateTimeOffset.UnixEpoch.AddSeconds(random.NextInt64(0, 1_000_000_000));
            yield return new QuotePayload(
                random.NextInt64(1, long.MaxValue),
                $"pickup-{random.NextInt64()}",
                $"dropoff-{random.NextInt64()}",
                issuedAt,
                issuedAt.AddSeconds(random.NextInt64(1, 86_400)),
                $"policy-{random.NextInt64()}",
                random.Next(0, 2) == 0 ? null : random.NextInt64(1, long.MaxValue),
                Currency(random),
                components,
                components.Sum(component => component.AmountMinor));
        }
    }

    private static string Key(Random random)
    {
        var bytes = new byte[32];
        random.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string Currency(Random random) =>
        new(Enumerable.Range(0, 3).Select(_ => (char)random.Next('A', 'Z' + 1)).ToArray());

    private static void AssertRejected(
        QuoteTokenCodec codec,
        string token,
        int position,
        char replacement)
    {
        var altered = token[..position] + replacement + token[(position + 1)..];
        var decode = () => codec.Decode(altered);
        decode.Should().Throw<InvalidQuoteTokenException>(
            $"position {position} changed from {token[position]} to {replacement}");
    }
}
