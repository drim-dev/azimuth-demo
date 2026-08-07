using FluentAssertions;
using Xunit;

namespace Pricing.Tests;

public sealed class QuoteTokenTests
{
    [Fact]
    public void Altering_any_signed_byte_invalidates_the_token()
    {
        var codec = new QuoteTokenCodec("test-key");
        var token = codec.Encode(new QuotePayload(
            1,
            "downtown",
            "airport",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch + TimeSpan.FromMinutes(2),
            "surge-v1",
            2,
            "EUR",
            [new("base", 500), new("distance", 1000), new("surge", 300)],
            1800));

        for (var index = 0; index < token.Length; index++)
        {
            if (token[index] == '.') continue;
            var replacement = token[index] == 'A' ? 'B' : 'A';
            var altered = token[..index] + replacement + token[(index + 1)..];
            Action decode = () => codec.Decode(altered);
            decode.Should().Throw<InvalidQuoteTokenException>();
        }
    }
}
