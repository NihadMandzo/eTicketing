using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ITicketTypeRepository : IRepository<TicketType, Guid>
{
    Task<List<TicketType>> GetBySectorIdAsync(Guid sectorId, CancellationToken ct = default);
}
