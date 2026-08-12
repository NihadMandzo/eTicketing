using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class EventRepository : Repository<Event, Guid>, IEventRepository
{
    public EventRepository(CatalogDbContext context) : base(context) { }

    public Task<PagedResult<Event>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, CancellationToken ct = default)
        => Query()
            .Include(e => e.Category)
            .Where(e => string.IsNullOrEmpty(query.FTS) || e.Name.Contains(query.FTS))
            .Where(e => organizationId == null || e.OrganizationId == organizationId)
            .Where(e => categoryId == null || e.CategoryId == categoryId)
            .OrderByDescending(e => e.Date)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<List<Guid>> GetOrganizationIdsByCategoryIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default)
        => Query()
            .Where(e => categoryIds.Contains(e.CategoryId))
            .Select(e => e.OrganizationId)
            .Distinct()
            .ToListAsync(ct);
}
