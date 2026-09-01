using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>A ticket is "live" when it can still be admitted at a gate: minted and paid for
/// (Confirmed), or minted, paid for and already PDF'd (Ready). Processing never got that far,
/// Cancelled was undone, Used has already walked through the door.</summary>
public interface ITicketRepository : IRepository<Ticket, Guid>
{
    /// <summary>Paged, scoped to one buyer — backs GET /tickets/mine. No SuperAdmin/Admin
    /// "all tickets" override yet (deliberately deferred, see .claude/rules/01-domain.md).</summary>
    Task<PagedResult<Ticket>> SearchByUserAsync(BaseSearchObject query, Guid userId, CancellationToken ct = default);

    /// <summary>TRACKED (not AsNoTracking) with Sector/TicketType included — the gate-validation
    /// path reads it, decides, and writes Status=Used on the same instance.</summary>
    Task<Ticket?> GetForValidationAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>Untracked, by id, with the navigations the printed ticket shows — the read-only
    /// sibling of <see cref="GetForValidationAsync"/>, which is tracked because it mutates.</summary>
    Task<Ticket?> GetForPdfAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>Tracked, by id — used by the TicketPdfReady consumer to flip Confirmed → Ready.</summary>
    Task<List<Ticket>> GetByIdsAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct = default);

    /// <summary>Per-product counts of live tickets admitting entry TODAY, for the organizer's
    /// validation list. <paramref name="organizationId"/> null means PlatformStaff (no org filter).
    /// SingleOccurrence rows carry no per-ticket date, so their "is it today" test needs
    /// Product.Date from Catalog and is applied by the caller — they come back unfiltered here and
    /// are marked with their TicketingMode so the caller knows which ones still need that check.</summary>
    Task<List<TicketValidationCounts>> GetValidationCountsAsync(Guid? organizationId, DateOnly today, CancellationToken ct = default);

    /// <summary>Distinct (UserId, UserEmail) of everyone still holding a live ticket for this
    /// product — the recipients of a "the event changed" email. Deduplicated here, so a buyer with
    /// three tickets to the same show gets one email, not three. Printed tickets are excluded by
    /// construction: they have no UserId and no address to write to.</summary>
    Task<List<TicketBuyer>> GetLiveBuyersForProductAsync(Guid productId, DateOnly today, CancellationToken ct = default);

    /// <summary>Highest stub number printed so far for this product, or 0 if none. Serial numbers
    /// run per product and never restart, so each new batch continues where the last one stopped
    /// and no two printed tickets for the same product ever show the same number.</summary>
    Task<int> GetMaxSerialNumberAsync(Guid productId, CancellationToken ct = default);

    /// <summary>One page of a print batch in stub-number order, untracked, with the navigations the
    /// printed sheet shows. The render worker pulls a batch through in chunks rather than
    /// materialising thousands of entities at once.</summary>
    Task<List<Ticket>> GetForPrintBatchAsync(Guid batchId, int skip, int take, CancellationToken ct = default);

    // ── Reporting aggregations (GET /reports/*) ──────────────────────────────────────────────
    // All five follow the convention GetValidationCountsAsync already established: a null
    // organizationId means "no org filter" (PlatformStaff), a non-null one scopes to that
    // organization via Sector.OrganizationId, which is denormalized onto Sector precisely so
    // ownership questions never need a cross-service call. [from, to] are inclusive local
    // calendar dates; the caller converts them to the half-open UTC instant range the CreatedAt
    // column is stored in, because comparing a DateTime column against a DateOnly can't be
    // translated to SQL.

    /// <summary>One row per UTC hour that saw any activity, ordered by hour.
    ///
    /// Hourly rather than daily on purpose. A local calendar day is what the report actually
    /// wants, but this layer cannot know what "local" means (see PlatformClock.ToLocal), and a
    /// UTC *day* cannot be converted to one afterwards — it straddles two local days. A UTC hour
    /// can: every DST transition falls on an hour boundary, so each of these rows belongs to
    /// exactly one local date, and ReportService folds them accordingly.
    ///
    /// Still an aggregate, not one row per ticket: the 366-day cap in ReportQueryValidator bounds
    /// this at 8,784 rows however many tickets were sold.</summary>
    Task<List<HourlySalesFacts>> GetHourlySalesAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    /// <summary>Per-product sales tallies for the range. Product names are not here — they live in
    /// Catalog and are resolved by the caller through ICatalogClient.</summary>
    Task<List<ProductSalesFacts>> GetProductSalesAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    /// <summary>Per-product check-in tallies. Sold counts every admittable ticket minted in the
    /// range; CheckedIn counts the ones that have actually walked through a gate (Status=Used),
    /// and Printed the box-office ones (Origin=Printed).</summary>
    Task<List<ProductRedemptionFacts>> GetRedemptionByProductAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    /// <summary>The UTC instant of every gate scan in the range, keyed on ValidatedAt (not
    /// CreatedAt) — this answers "when do people actually arrive", which is a different question
    /// from when they bought, so a ticket sold in June and scanned in August belongs to August.
    ///
    /// Returns the raw instants rather than an hour histogram because the hour that matters is the
    /// *local* one, and only the Business layer can convert (see PlatformClock.ToLocal).</summary>
    Task<List<DateTime>> GetCheckinTimestampsAsync(
        Guid? organizationId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    /// <summary>Per-organization sales tallies — the platform-staff Organizations tab. Called
    /// twice by the service (current range and the immediately preceding equal-length one) to
    /// compute period-over-period growth.</summary>
    Task<List<OrganizationSalesFacts>> GetOrganizationSalesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);

    /// <summary>Current sold count per product, unscoped by any date range — unlike
    /// <see cref="GetProductSalesAsync"/> (which counts tickets *purchased* inside a reporting
    /// window), this answers "how many of this future event's tickets are sold right now,
    /// regardless of when they were bought", for the Dashboard's "Nadolazeći događaji" sold/capacity
    /// bar. Products with zero sales are simply absent, not a zero row.</summary>
    Task<List<ProductSoldCount>> GetSoldCountsByProductIdsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken ct = default);
}

/// <summary>Projection, not an entity — one row per (product, mode) with today's ticket tallies.</summary>
public record TicketValidationCounts(Guid ProductId, TicketingMode TicketingMode, int TotalToday, int ValidatedToday);

public record TicketBuyer(Guid UserId, string UserEmail);

// ── Reporting projections ────────────────────────────────────────────────────────────────────
// Cancelled tickets are counted separately everywhere rather than folded into Sold/Revenue: a
// cancelled ticket was a real sale that was then undone, so the reports show both the gross
// figure and what came off it, never a silently netted number.

/// <summary>Sales for one UTC hour, split into what stands and what was cancelled.
/// <paramref name="HourUtc"/> is truncated to the hour.</summary>
public record HourlySalesFacts(
    DateTime HourUtc, int Sold, decimal Revenue, int Cancelled, decimal CancelledAmount, int OnlineSold, int PrintedSold);

/// <summary>Sales for one product over the whole reporting range.</summary>
public record ProductSalesFacts(Guid ProductId, int Sold, decimal Revenue, int Cancelled, decimal CancelledAmount);

/// <summary>Gate usage for one product over the whole reporting range.</summary>
public record ProductRedemptionFacts(Guid ProductId, int Sold, int CheckedIn, int Printed);

/// <summary>Sales for one organization over the whole reporting range.</summary>
public record OrganizationSalesFacts(Guid OrganizationId, int Sold, decimal Revenue);

/// <summary>Current sold count for one product, unscoped by purchase date.</summary>
public record ProductSoldCount(Guid ProductId, int Sold);
