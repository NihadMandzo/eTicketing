using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default);

    /// <summary>Username ownership check that excludes <paramref name="excludeUserId"/>, so a user renaming themselves back to their own current username never trips a "taken" conflict.</summary>
    Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default);
}
