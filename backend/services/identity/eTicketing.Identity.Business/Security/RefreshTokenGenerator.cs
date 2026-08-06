using System.Security.Cryptography;
using System.Text;

namespace eTicketing.Identity.Business.Security;

/// <summary>
/// Generates opaque refresh tokens and their storage hash. Unlike <see cref="PasswordHasher"/>,
/// this doesn't need a slow, salted KDF — the raw token is already 512 bits of random data,
/// so a fast deterministic hash (SHA-256) is enough to look it up by hash in storage while
/// never persisting the raw value.
/// </summary>
public static class RefreshTokenGenerator
{
    public static string GenerateRaw() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
