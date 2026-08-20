using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ITicketRepository : IRepository<Ticket, Guid>
{
    /// <summary>Paged, scoped to one buyer — backs GET /tickets/mine. No SuperAdmin/Admin
    /// "all tickets" override yet (deliberately deferred, see .claude/rules/01-domain.md).</summary>
    Task<PagedResult<Ticket>> SearchByUserAsync(BaseSearchObject query, Guid userId, CancellationToken ct = default);
}
