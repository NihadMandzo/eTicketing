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
            .Include(s => s.TicketTypes)
            .Where(s => string.IsNullOrEmpty(query.FTS) || s.Name.Contains(query.FTS))
            .Where(s => productId == null || s.ProductId == productId)
            .Where(s => organizationId == null || s.OrganizationId == organizationId)
            .Where(s => status == null || s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Sector?> GetByIdWithTicketTypesAsync(Guid id, CancellationToken ct = default)
        => Query().Include(s => s.TicketTypes).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<ProductCapacity>> GetPublishedCapacityByProductAsync(
        IReadOnlyList<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return Task.FromResult(new List<ProductCapacity>());

        return Query()
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId))
            .Where(s => s.Status == PublishStatus.Published)
            .GroupBy(s => s.ProductId)
            .Select(g => new ProductCapacity(g.Key, g.Sum(s => s.Capacity), g.Count()))
            .ToListAsync(ct);
    }
}
