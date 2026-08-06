using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class RefreshTokenRepository : Repository<RefreshToken, int>, IRefreshTokenRepository
{
    public RefreshTokenRepository(IdentityDbContext context) : base(context) { }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        // AsNoTracking: this is a read-only lookup, and TryRevokeAsync mutates rows via
        // ExecuteUpdateAsync, which bypasses the change tracker entirely. A tracked result here
        // would let a later call on the same DbContext see a stale, pre-revoke RevokedAt/
        // ReplacedByTokenHash instead of what TryRevokeAsync actually wrote.
        => Query().AsNoTracking().Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await Query()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    public async Task<bool> TryRevokeAsync(string tokenHash, string replacedByTokenHash, DateTime revokedAt, CancellationToken ct = default)
    {
        var affected = await Query()
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, revokedAt)
                .SetProperty(t => t.ReplacedByTokenHash, replacedByTokenHash), ct);

        return affected == 1;
    }

    public async Task RevokeAsync(string tokenHash, DateTime revokedAt, CancellationToken ct = default)
    {
        await Query()
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, revokedAt), ct);
    }
}
