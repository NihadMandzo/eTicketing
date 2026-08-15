using eTicketing.Contracts.Persistence;

namespace eTicketing.Identity.Data.Entities;

/// <summary>
/// A single-use, hashed password-reset token emailed to a buyer who requested a reset.
/// <see cref="Id"/> is a plain int — purely an internal DB identity, never serialized to any
/// client (the client only ever sees the opaque raw token via the emailed link). Mirrors
/// <see cref="RefreshToken"/>'s raw-token + SHA-256-hash storage pattern via
/// <see cref="eTicketing.Identity.Business.Security.RefreshTokenGenerator"/>, but is single-use
/// (<see cref="UsedAt"/>) rather than rotated.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hash of the raw token — the raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment this token is consumed by a successful reset, or superseded by a
    /// newer reset request — either way, once set the token can never be honored again.</summary>
    public DateTime? UsedAt { get; set; }

    public bool IsValid => UsedAt is null && DateTime.UtcNow < ExpiresAt;
}
