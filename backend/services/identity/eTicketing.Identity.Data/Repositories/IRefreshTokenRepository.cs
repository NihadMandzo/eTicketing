using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshToken, int>
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Atomically revokes the token identified by <paramref name="tokenHash"/> and records its
    /// successor, but only if it is still unrevoked at the moment the UPDATE runs. Backed by a
    /// single conditional <c>UPDATE ... WHERE TokenHash = @hash AND RevokedAt IS NULL</c>
    /// (via <c>ExecuteUpdateAsync</c>), so two concurrent refreshes of the same token can never
    /// both win — the second one gets 0 affected rows and must fail instead of minting a second
    /// successor token. Returns <see langword="true"/> only for the caller that won the race.
    /// </summary>
    Task<bool> TryRevokeAsync(string tokenHash, string replacedByTokenHash, DateTime revokedAt, CancellationToken ct = default);

    /// <summary>
    /// Revokes the token identified by <paramref name="tokenHash"/> (a plain logout — no
    /// successor token), only if it is not already revoked. Same conditional-UPDATE shape as
    /// <see cref="TryRevokeAsync"/>, committed immediately rather than through <c>SaveChanges</c>.
    /// A no-op (0 rows affected) for an unknown or already-revoked token hash.
    /// </summary>
    Task RevokeAsync(string tokenHash, DateTime revokedAt, CancellationToken ct = default);
}
