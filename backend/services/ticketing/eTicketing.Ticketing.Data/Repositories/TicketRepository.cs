using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class TicketRepository : Repository<Ticket, Guid>, ITicketRepository
{
    private static readonly TicketStatus[] LiveStatuses = [TicketStatus.Confirmed, TicketStatus.Ready];

    public TicketRepository(TicketingDbContext context) : base(context) { }

    public Task<PagedResult<Ticket>> SearchByUserAsync(BaseSearchObject query, Guid userId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(t => t.Sector)
            .Include(t => t.TicketType)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Ticket?> GetForValidationAsync(Guid ticketId, CancellationToken ct = default)
        => Query()
            .Include(t => t.Sector)
            .Include(t => t.TicketType)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

    public Task<Ticket?> GetForPdfAsync(Guid ticketId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(t => t.Sector)
            .Include(t => t.TicketType)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

    public Task<List<Ticket>> GetByIdsAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct = default)
        => Query().Where(t => ticketIds.Contains(t.Id)).ToListAsync(ct);

    public Task<List<TicketValidationCounts>> GetValidationCountsAsync(Guid? organizationId, DateOnly today, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(t => organizationId == null || t.Sector!.OrganizationId == organizationId)
            // Cancelled/Processing can never be admitted; Used already was, but still counts
            // toward today's tally so the organizer sees "validirano 12 / 40" rather than a
            // silently shrinking total as the queue moves.
            .Where(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used)
            // "Admits entry today", by mode, inlined rather than extracted to a helper because EF
            // Core can only translate an expression tree it can see through — a private static
            // bool method here would throw at query time instead of becoming SQL. DailyEntry pins
            // an exact ValidDate; RecurringReservation spans ValidFrom..ValidTo; SingleOccurrence
            // has neither (its showing date lives on Catalog's Product.Date), so it always passes
            // here and is filtered against the real product date by the caller.
            .Where(t => t.ValidDate == null || t.ValidDate == today)
            .Where(t => t.ValidFrom == null || t.ValidFrom <= today)
            .Where(t => t.ValidTo == null || t.ValidTo >= today)
            .GroupBy(t => new { t.ProductId, t.Sector!.TicketingMode })
            .Select(g => new TicketValidationCounts(
                g.Key.ProductId,
                g.Key.TicketingMode,
                g.Count(),
                g.Count(t => t.Status == TicketStatus.Used)))
            .ToListAsync(ct);

    public Task<List<TicketBuyer>> GetLiveBuyersForProductAsync(Guid productId, DateOnly today, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(t => t.ProductId == productId)
            .Where(t => LiveStatuses.Contains(t.Status))
            // Don't mail someone whose day pass was for last Tuesday or whose parking period
            // already closed — the change can't affect them any more.
            .Where(t => t.ValidDate == null || t.ValidDate >= today)
            .Where(t => t.ValidTo == null || t.ValidTo >= today)
            // Printed tickets carry no buyer — there is nobody to notify, and projecting them
            // would put a null address into the notification fan-out.
            .Where(t => t.UserId != null && t.UserEmail != null)
            .Select(t => new TicketBuyer(t.UserId!.Value, t.UserEmail!))
            .Distinct()
            .ToListAsync(ct);

    public async Task<int> GetMaxSerialNumberAsync(Guid productId, CancellationToken ct = default)
        => await Query()
            .AsNoTracking()
            .Where(t => t.ProductId == productId && t.SerialNumber != null)
            .MaxAsync(t => (int?)t.SerialNumber, ct) ?? 0;

    public Task<List<Ticket>> GetForPrintBatchAsync(Guid batchId, int skip, int take, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(t => t.Sector)
            .Include(t => t.TicketType)
            .Where(t => t.PrintBatchId == batchId)
            .OrderBy(t => t.SerialNumber)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
}
