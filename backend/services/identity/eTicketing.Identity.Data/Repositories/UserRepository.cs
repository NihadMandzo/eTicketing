using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(IdentityDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Email == email || u.Username == username, ct);
}
