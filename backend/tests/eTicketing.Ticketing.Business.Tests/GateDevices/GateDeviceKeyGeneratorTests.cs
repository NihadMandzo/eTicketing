using eTicketing.Ticketing.Business.Security;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.GateDevices;

public class GateDeviceKeyGeneratorTests
{
    private readonly GateDeviceKeyGenerator _sut = new();

    [Fact]
    public void Create_ProducesAPrefixedKeyWhosePrefixMatchesTheStoredDisplayPrefix()
    {
        var key = _sut.Create();

        key.ApiKey.Should().StartWith(GateDeviceKeyGenerator.Prefix);
        key.KeyPrefix.Should().Be(key.ApiKey[..GateDeviceKeyGenerator.DisplayPrefixLength]);
    }

    [Fact]
    public void Create_ProducesAKeySafeToCarryInAnHttpHeaderAndAFirmwareConstant()
    {
        // Base64url, so no '+', '/' or '=' to be mangled by a serial console, a header parser, or a
        // C string literal on the way to the device.
        var key = _sut.Create();

        key.ApiKey.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void Create_ProducesADistinctKeyEveryCall()
    {
        var keys = Enumerable.Range(0, 200).Select(_ => _sut.Create().ApiKey).ToList();

        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Create_HashesTheKeyItReturns()
    {
        var key = _sut.Create();

        key.KeyHash.Should().Be(GateDeviceKeyGenerator.Hash(key.ApiKey));
        key.KeyHash.Should().NotContain(key.ApiKey);
    }

    [Fact]
    public void Hash_IsStableAcrossCalls()
    {
        // The hash is the lookup key on the authentication path — an unstable encoding would lock
        // every registered device out at once.
        var key = _sut.Create();

        GateDeviceKeyGenerator.Hash(key.ApiKey).Should().Be(GateDeviceKeyGenerator.Hash(key.ApiKey));
    }

    [Fact]
    public void Hash_IsLowercaseHexOfExactlySha256Length()
    {
        // The column is nchar(64); anything else would be silently truncated or padded.
        GateDeviceKeyGenerator.Hash("etk_gate_whatever").Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Theory]
    [InlineData("etk_gate_aaaa", "etk_gate_aaab")]
    [InlineData("etk_gate_AAAA", "etk_gate_aaaa")]
    public void Hash_DiffersForDifferentKeys(string first, string second)
    {
        GateDeviceKeyGenerator.Hash(first).Should().NotBe(GateDeviceKeyGenerator.Hash(second));
    }
}
