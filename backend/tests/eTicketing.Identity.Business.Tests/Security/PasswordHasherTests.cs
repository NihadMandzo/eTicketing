using eTicketing.Identity.Business.Security;
using FluentAssertions;

namespace eTicketing.Identity.Business.Tests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_WithCorrectPassword_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("MySecretPassword123");

        PasswordHasher.Verify("MySecretPassword123", hash, salt).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_Fails()
    {
        var (hash, salt) = PasswordHasher.Hash("MySecretPassword123");

        PasswordHasher.Verify("SomeOtherPassword", hash, salt).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashesAndSalts()
    {
        var (hash1, salt1) = PasswordHasher.Hash("SamePassword");
        var (hash2, salt2) = PasswordHasher.Hash("SamePassword");

        hash1.Should().NotBe(hash2);
        salt1.Should().NotBe(salt2);
    }
}
