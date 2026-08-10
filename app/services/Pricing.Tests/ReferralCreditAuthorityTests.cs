using Common.Referrals;
using FluentAssertions;
using Xunit;

namespace Pricing.Tests;

public sealed class ReferralCreditAuthorityTests
{
    private readonly ReferralCreditAuthorityCodec _codec = new("referral-test-signing-key");

    [Fact]
    public void Authority_round_trips_exact_binding()
    {
        var authority = new ReferralCreditAuthority(41, 73, 500, "USD");

        _codec.Decode(_codec.Encode(authority)).Should().Be(authority);
    }

    [Fact]
    public void Altered_authority_is_rejected()
    {
        var token = _codec.Encode(new ReferralCreditAuthority(41, 73, 500, "USD"));
        var altered = $"{(token[0] == 'a' ? 'b' : 'a')}{token[1..]}";

        var decode = () => _codec.Decode(altered);

        decode.Should().Throw<InvalidReferralCreditAuthorityException>();
    }

    [Fact]
    public void Foreign_authority_is_rejected()
    {
        var foreign = new ReferralCreditAuthorityCodec("foreign-key")
            .Encode(new ReferralCreditAuthority(41, 73, 500, "USD"));

        var decode = () => _codec.Decode(foreign);

        decode.Should().Throw<InvalidReferralCreditAuthorityException>();
    }
}
