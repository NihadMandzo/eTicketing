using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(IdentityDbContext context) : base(context) { }

    public Task<PagedResult<Organization>> SearchAsync(BaseSearchObject query, CancellationToken ct = default)
        => Query()
            .Where(o => string.IsNullOrEmpty(query.FTS) || o.Name.Contains(query.FTS))
            .Include(o => o.Users)
            .OrderBy(o => o.Name)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Organization?> GetByIdWithUsersAsync(Guid id, CancellationToken ct = default)
        => Query().Include(o => o.Users).FirstOrDefaultAsync(o => o.Id == id, ct);
}
