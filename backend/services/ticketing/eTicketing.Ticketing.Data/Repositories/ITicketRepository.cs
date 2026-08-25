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
    /// three tickets to the same show gets one email, not three.</summary>
    Task<List<TicketBuyer>> GetLiveBuyersForProductAsync(Guid productId, DateOnly today, CancellationToken ct = default);
}

/// <summary>Projection, not an entity — one row per (product, mode) with today's ticket tallies.</summary>
public record TicketValidationCounts(Guid ProductId, TicketingMode TicketingMode, int TotalToday, int ValidatedToday);

public record TicketBuyer(Guid UserId, string UserEmail);
