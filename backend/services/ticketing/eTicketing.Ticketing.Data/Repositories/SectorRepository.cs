using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class SectorRepository : Repository<Sector, Guid>, ISectorRepository
{
    private readonly TicketingDbContext _context;

    public SectorRepository(TicketingDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<PagedResult<Sector>> SearchAsync(
        BaseSearchObject query, Guid? productId, Guid? organizationId, PublishStatus? status,
        bool requirePublishedProduct, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(s => s.TicketTypes)
            .Where(s => string.IsNullOrEmpty(query.FTS) || s.Name.Contains(query.FTS))
            .Where(s => productId == null || s.ProductId == productId)
            .Where(s => organizationId == null || s.OrganizationId == organizationId)
            .Where(s => status == null || s.Status == status)
            // Translates to an EXISTS, so it filters and counts in one round trip. A product with
            // no snapshot row at all fails it, which is the safe direction — see ProductSnapshot.
            .Where(s => !requirePublishedProduct || _context.ProductSnapshots
                .Any(p => p.ProductId == s.ProductId && p.Status == PublishStatus.Published))
            .OrderByDescending(s => s.CreatedAt)
            .ToPagedResultAsync(query.EffectivePage, query.EffectivePageSize, ct);

    public Task<Sector?> GetByIdWithTicketTypesAsync(Guid id, CancellationToken ct = default)
        => Query().Include(s => s.TicketTypes).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Sector>> GetPublishedByProductWithTicketTypesAsync(Guid productId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(s => s.TicketTypes)
            .Where(s => s.ProductId == productId && s.Status == PublishStatus.Published)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    // No AsNoTracking here — see ISectorRepository for why this one has to stay tracked.
    public Task<List<Sector>> GetByIdsWithTicketTypesAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return Task.FromResult(new List<Sector>());

        return Query()
            .Include(s => s.TicketTypes)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(ct);
    }

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
