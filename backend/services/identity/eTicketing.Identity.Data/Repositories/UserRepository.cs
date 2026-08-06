using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class UserRepository : Repository<User, Guid>, IUserRepository
{
    public UserRepository(IdentityDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Query().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Query().FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Email == email || u.Username == username, ct);

    public Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Username == username && u.Id != excludeUserId, ct);
}
