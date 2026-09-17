using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class GateDeviceRepository : Repository<GateDevice, Guid>, IGateDeviceRepository
{
    private readonly TicketingDbContext _context;

    public GateDeviceRepository(TicketingDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<GateDevice?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
        => Query()
            .Include(d => d.Sectors)
            .FirstOrDefaultAsync(d => d.KeyHash == keyHash, ct);

    public Task<PagedResult<GateDevice>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, Guid? productId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(d => d.Sectors)
                .ThenInclude(s => s.Sector)
            .Where(d => string.IsNullOrEmpty(query.FTS) || d.Name.Contains(query.FTS))
            .Where(d => organizationId == null || d.OrganizationId == organizationId)
            .Where(d => productId == null || d.ProductId == productId)
            .OrderByDescending(d => d.CreatedAt)
            .ToPagedResultAsync(query.EffectivePage, query.EffectivePageSize, ct);

    public Task<GateDevice?> GetByIdWithSectorsAsync(Guid id, CancellationToken ct = default)
        => Query()
            .Include(d => d.Sectors)
                .ThenInclude(s => s.Sector)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Guid>> GetSectorIdsForProductAsync(Guid productId, IReadOnlyList<Guid> sectorIds, CancellationToken ct = default)
        => _context.Sectors
            .AsNoTracking()
            .Where(s => s.ProductId == productId && sectorIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);

    public void AddSector(GateDeviceSector sector) => _context.GateDeviceSectors.Add(sector);

    public void RemoveSector(GateDeviceSector sector) => _context.GateDeviceSectors.Remove(sector);

    public async Task<List<(Guid Id, string Name)>> GetSectorNamesAsync(IReadOnlyList<Guid> sectorIds, CancellationToken ct = default)
    {
        // Projected to an anonymous type first: EF Core can't translate a ValueTuple constructor
        // into SQL, so the tuple has to be built client-side after materialization.
        var rows = await _context.Sectors
            .AsNoTracking()
            .Where(s => sectorIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        return rows.Select(r => (r.Id, r.Name)).ToList();
    }
}
