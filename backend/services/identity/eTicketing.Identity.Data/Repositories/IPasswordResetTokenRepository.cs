using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken, int>
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Marks every still-valid (unused, unexpired) token for this user as used — called
    /// before issuing a new one, so requesting a fresh reset link immediately invalidates any
    /// older one still sitting in an inbox.</summary>
    Task InvalidateAllActiveForUserAsync(Guid userId, CancellationToken ct = default);
}
