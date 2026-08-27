using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace eTicketing.Ticketing.Business.Security;

/// <summary>
/// Mints and hashes the API key an unattended gate scanner authenticates with:
/// <c>etk_gate_{32 base64url chars}</c>, backed by 24 bytes (192 bits) of CSPRNG output.
///
/// Stored as plain SHA-256 rather than a password KDF, deliberately. A KDF's cost factor exists to
/// make guessing a low-entropy human-chosen secret expensive; there is nothing to guess at in 192
/// random bits, and the hash sits on the authentication path of every single scan at a gate with a
/// queue behind it. The property that actually matters here — a database leak must not yield usable
/// keys — is what the one-way hash provides.
///
/// The prefix is ASCII and free of '+' and '/' so it survives being typed into a firmware header,
/// pasted through a serial console, or carried in an HTTP header untouched.
/// </summary>
public sealed class GateDeviceKeyGenerator
{
    public const string Prefix = "etk_gate_";

    /// <summary>How much of the key is kept in the clear for display. Long enough that two devices
    /// registered in the same minute are still distinguishable, far too short to brute-force the
    /// remainder.</summary>
    public const int DisplayPrefixLength = 16;

    private const int SecretBytes = 24;

    public GateDeviceKey Create()
    {
        var key = Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return new GateDeviceKey(key, Hash(key), key[..DisplayPrefixLength]);
    }

    /// <summary>Lowercase hex SHA-256. Also the lookup key on the authentication path, so the
    /// encoding has to stay byte-stable — never change the casing or switch to base64.</summary>
    public static string Hash(string apiKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}

/// <summary><paramref name="ApiKey"/> is the only time the plaintext exists — it is returned to the
/// organizer once and then only its hash survives.</summary>
public sealed record GateDeviceKey(string ApiKey, string KeyHash, string KeyPrefix);
