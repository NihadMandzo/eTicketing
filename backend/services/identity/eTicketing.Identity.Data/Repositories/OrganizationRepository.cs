using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(IdentityDbContext context) : base(context) { }

    public Task<PagedResult<Organization>> SearchAsync(BaseSearchObject query, IReadOnlyList<Guid>? organizationIds, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(o => string.IsNullOrEmpty(query.FTS) || o.Name.Contains(query.FTS))
            // Minimal APIs bind an absent array-typed query param to an empty array, not null —
            // organizationIds.Count == 0 must also mean "no filter", or every organization gets
            // excluded whenever the category multiselect filter isn't in use.
            .Where(o => organizationIds == null || organizationIds.Count == 0 || organizationIds.Contains(o.Id))
            .Include(o => o.Users)
            .OrderBy(o => o.Name)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Organization?> GetByIdWithUsersAsync(Guid id, CancellationToken ct = default)
        => Query().Include(o => o.Users).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Organization>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToListAsync(ct);
}
