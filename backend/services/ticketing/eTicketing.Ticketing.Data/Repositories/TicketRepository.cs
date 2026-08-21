using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class TicketRepository : Repository<Ticket, Guid>, ITicketRepository
{
    public TicketRepository(TicketingDbContext context) : base(context) { }

    public Task<PagedResult<Ticket>> SearchByUserAsync(BaseSearchObject query, Guid userId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(t => t.Sector)
            .Include(t => t.TicketType)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
}
