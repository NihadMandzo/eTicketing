using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default);
}
