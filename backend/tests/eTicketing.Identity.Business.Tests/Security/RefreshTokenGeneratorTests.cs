using eTicketing.Identity.Business.Security;
using FluentAssertions;

namespace eTicketing.Identity.Business.Tests.Security;

public class RefreshTokenGeneratorTests
{
    [Fact]
    public void GenerateRaw_ProducesUniqueTokensAcrossCalls()
    {
        var first = RefreshTokenGenerator.GenerateRaw();
        var second = RefreshTokenGenerator.GenerateRaw();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Hash_IsDeterministic_ForTheSameRawToken()
    {
        var raw = RefreshTokenGenerator.GenerateRaw();

        RefreshTokenGenerator.Hash(raw).Should().Be(RefreshTokenGenerator.Hash(raw));
    }

    [Fact]
    public void Hash_DiffersForDifferentRawTokens()
    {
        var first = RefreshTokenGenerator.Hash("token-a");
        var second = RefreshTokenGenerator.Hash("token-b");

        first.Should().NotBe(second);
    }
}
