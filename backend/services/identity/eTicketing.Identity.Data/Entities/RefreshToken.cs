using eTicketing.Contracts.Persistence;

namespace eTicketing.Identity.Data.Entities;

/// <summary>
/// A rotated, hashed refresh token backing a user's httpOnly-cookie session.
/// <see cref="Id"/> is a plain int — it is purely an internal DB identity and is never
/// serialized to any client (the client only ever sees the opaque raw token via the
/// httpOnly cookie, which is unrelated to this row's Id).
/// </summary>
public class RefreshToken : BaseEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hash of the raw token — the raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Set when this token is rotated away, so reuse of a revoked token can be detected.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
