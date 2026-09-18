using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class ProductSnapshotRepository : Repository<ProductSnapshot, Guid>, IProductSnapshotRepository
{
    private readonly TicketingDbContext _context;

    public ProductSnapshotRepository(TicketingDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<ProductSnapshot?> GetByIdNoTrackingAsync(Guid productId, CancellationToken ct = default)
        => Query().AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct);

    public Task<List<ProductSnapshot>> GetByIdsNoTrackingAsync(IReadOnlyList<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return Task.FromResult(new List<ProductSnapshot>());

        return Query().AsNoTracking().Where(p => productIds.Contains(p.ProductId)).ToListAsync(ct);
    }

    public Task<List<Guid>> GetProductIdsMissingSnapshotAsync(CancellationToken ct = default)
        => _context.Sectors
            .AsNoTracking()
            .Select(s => s.ProductId)
            .Distinct()
            .Where(productId => !_context.ProductSnapshots.Any(p => p.ProductId == productId))
            .ToListAsync(ct);
}
