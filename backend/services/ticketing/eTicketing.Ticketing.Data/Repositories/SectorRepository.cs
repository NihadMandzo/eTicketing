using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class SectorRepository : Repository<Sector, Guid>, ISectorRepository
{
    public SectorRepository(TicketingDbContext context) : base(context) { }

    public Task<PagedResult<Sector>> SearchAsync(
        BaseSearchObject query, Guid? productId, Guid? organizationId, PublishStatus? status, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(s => string.IsNullOrEmpty(query.FTS) || s.Name.Contains(query.FTS))
            .Where(s => productId == null || s.ProductId == productId)
            .Where(s => organizationId == null || s.OrganizationId == organizationId)
            .Where(s => status == null || s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
}
