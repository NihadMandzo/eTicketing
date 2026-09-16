using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.Sectors;

public record SectorResponse(
    Guid Id,
    Guid ProductId,
    string Name,
    int Capacity,
    decimal Price,
    PublishStatus Status,
    TicketingMode TicketingMode,
    int? PeriodYear,
    int? PeriodMonth,
    DateTime CreatedAt,
    IReadOnlyList<TicketTypeResponse> TicketTypes,
    /// <summary>How many admissions are still on sale, read off the live Redis counter — what the
    /// buying clients need to grey a sold-out option out instead of letting someone pick it and
    /// collect a 409 at hold time. Populated on the public buyer path only (GET /sectors); the
    /// organizer/staff lists leave it null rather than pay a Redis round-trip per row for a number
    /// their screens don't show. Also null for a DailyEntry sector queried without a date, because
    /// that mode counts capacity per (Sector, date) and a single number would be a lie.</summary>
    int? RemainingCapacity = null)
{
    /// <summary>True only when availability is actually known and exhausted. An unknown
    /// (null) remaining must never read as sold out — that would hide sectors that are on sale.
    /// </summary>
    public bool IsSoldOut => RemainingCapacity == 0;
}
