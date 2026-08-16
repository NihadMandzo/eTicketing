using System.Security.Cryptography;
using System.Text;

namespace eTicketing.Identity.Business.Security;

/// <summary>
/// Generates opaque single-use tokens and their storage hash. Unlike <see cref="PasswordHasher"/>,
/// this doesn't need a slow, salted KDF — the raw token is already 512 bits of random data,
/// so a fast deterministic hash (SHA-256) is enough to look it up by hash in storage while
/// never persisting the raw value. Despite the name, this backs both <c>RefreshToken</c> and
/// <c>PasswordResetToken</c> — the generation/hashing scheme is identical for both, only the
/// entity storing the hash and its expiry/reuse semantics differ.
/// </summary>
public static class RefreshTokenGenerator
{
    public static string GenerateRaw() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
