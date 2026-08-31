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

    // ── Reporting aggregations ───────────────────────────────────────────────────────────────
    //
    // Every status test below is written out as explicit == comparisons rather than pulled from
    // the LiveStatuses array above, for the same reason GetValidationCountsAsync inlines its
    // date logic: EF Core can only translate an expression tree it can see through, and an array
    // .Contains() nested inside an aggregate (g.Count(t => ...)) is not reliably translated
    // across both the SQL Server provider and the Sqlite one the tests run on.
    //
    // "Sold" means Confirmed, Ready or Used — a sale that stands, wherever the ticket is in its
    // lifecycle. Processing never completed and is excluded from every figure; Cancelled is
    // counted on its own so the reports can show gross and what came off it, never a silently
    // netted number.

    public async Task<List<HourlySalesFacts>> GetHourlySalesAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
    {
        var rows = await ReportScope(organizationId, fromUtc, toUtcExclusive)
            // Grouped by UTC date + UTC hour, never by UTC *day* alone. A day boundary here is not
            // a day boundary in the platform's own time zone, and once rows are folded into a UTC
            // day the split is unrecoverable — the caller could no longer tell which of the two
            // local days each ticket belonged to. An hour survives that conversion intact, because
            // every DST transition happens on an hour boundary.
            //
            // Date and Hour are separate keys rather than one truncated DateTime because that is
            // what both providers translate: DATEPART on SQL Server, strftime on Sqlite. (The
            // equivalent on a *nullable* column does not translate at all — see
            // GetCheckinTimestampsAsync, which is why that one materialises instead.)
            .GroupBy(t => new { Date = t.CreatedAt.Date, Hour = t.CreatedAt.Hour })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.Hour,
                Sold = g.Count(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used),
                Revenue = g.Sum(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used
                    ? t.PricePaid
                    : 0m),
                Cancelled = g.Count(t => t.Status == TicketStatus.Cancelled),
                CancelledAmount = g.Sum(t => t.Status == TicketStatus.Cancelled ? t.PricePaid : 0m),
            })
            .ToListAsync(ct);

        // Recombining date+hour happens here rather than in the projection for the same reason
        // DateOnly.FromDateTime used to: AddHours inside a Select is not something to rely on
        // across providers, and the row count is bounded at 8,784 by the 366-day range cap.
        return [.. rows
            .Select(r => new HourlySalesFacts(
                DateTime.SpecifyKind(r.Date.AddHours(r.Hour), DateTimeKind.Utc),
                r.Sold, r.Revenue, r.Cancelled, r.CancelledAmount))
            .OrderBy(r => r.HourUtc)];
    }

    public Task<List<ProductSalesFacts>> GetProductSalesAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
        => ReportScope(organizationId, fromUtc, toUtcExclusive)
            .GroupBy(t => t.ProductId)
            .Select(g => new ProductSalesFacts(
                g.Key,
                g.Count(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used),
                g.Sum(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used
                    ? t.PricePaid
                    : 0m),
                g.Count(t => t.Status == TicketStatus.Cancelled),
                g.Sum(t => t.Status == TicketStatus.Cancelled ? t.PricePaid : 0m)))
            .ToListAsync(ct);

    public Task<List<ProductRedemptionFacts>> GetRedemptionByProductAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
        => ReportScope(organizationId, fromUtc, toUtcExclusive)
            .GroupBy(t => t.ProductId)
            .Select(g => new ProductRedemptionFacts(
                g.Key,
                g.Count(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used),
                g.Count(t => t.Status == TicketStatus.Used),
                // Printed stubs that were cancelled are not "printed tickets in circulation", so
                // the same sold-status filter applies here as everywhere else.
                g.Count(t => t.Origin == TicketOrigin.Printed
                    && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used))))
            .ToListAsync(ct);

    public Task<List<DateTime>> GetCheckinTimestampsAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
        // The one reporting query here that does not aggregate in SQL, for two reasons. Grouping
        // on ValidatedAt.Value.Hour is untranslatable — no provider maps the Hour component of a
        // *nullable* DateTime, and the query fails outright rather than falling back. And the hour
        // the report wants is the local one, which this layer cannot compute at all (see
        // PlatformClock.ToLocal), so bucketing here would only produce the wrong answer faster.
        //
        // One narrow column, no entity materialisation: the caller gets the instants and decides
        // what hour each one falls in.
        => Query()
            .AsNoTracking()
            .Where(t => organizationId == null || t.Sector!.OrganizationId == organizationId)
            // Keyed on ValidatedAt, not CreatedAt: this chart answers "when do people arrive",
            // which is a different question from when they bought, so a ticket bought in June and
            // scanned in August belongs to August's histogram.
            .Where(t => t.ValidatedAt != null && t.ValidatedAt >= fromUtc && t.ValidatedAt < toUtcExclusive)
            .Select(t => t.ValidatedAt!.Value)
            .ToListAsync(ct);

    public Task<List<OrganizationSalesFacts>> GetOrganizationSalesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
        => ReportScope(organizationId: null, fromUtc, toUtcExclusive)
            .GroupBy(t => t.Sector!.OrganizationId)
            .Select(g => new OrganizationSalesFacts(
                g.Key,
                g.Count(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used),
                g.Sum(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Ready || t.Status == TicketStatus.Used
                    ? t.PricePaid
                    : 0m)))
            .ToListAsync(ct);

    /// <summary>The window every report aggregation starts from: tickets minted inside the range,
    /// optionally narrowed to one organization, with Processing rows dropped up front so no
    /// downstream projection has to remember to exclude them. The upper bound is exclusive so a
    /// ticket bought at 23:59:59.9 on the last day still counts.</summary>
    private IQueryable<Ticket> ReportScope(Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive)
        => Query()
            .AsNoTracking()
            .Where(t => organizationId == null || t.Sector!.OrganizationId == organizationId)
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtcExclusive)
            .Where(t => t.Status != TicketStatus.Processing);
}
