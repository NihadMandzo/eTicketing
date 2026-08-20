using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class TicketTypeRepository : Repository<TicketType, Guid>, ITicketTypeRepository
{
    public TicketTypeRepository(TicketingDbContext context) : base(context) { }

    public Task<List<TicketType>> GetBySectorIdAsync(Guid sectorId, CancellationToken ct = default)
        => Query().AsNoTracking().Where(t => t.SectorId == sectorId).OrderBy(t => t.CreatedAt).ToListAsync(ct);
}
