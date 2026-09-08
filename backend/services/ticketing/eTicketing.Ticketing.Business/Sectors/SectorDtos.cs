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

/// <summary>Returned by both POST /sectors/preview (stateless, no DB write) and as the shape
/// PreviewAsync/CreateAsync validate against — see SectorService.ValidateAsync().</summary>
public record SectorPreviewResponse(
    Guid ProductId,
    string Name,
    int Capacity,
    decimal Price,
    TicketingMode TicketingMode,
    int? PeriodYear,
    int? PeriodMonth);

/// <summary>Same shape for create and update.</summary>
public record UpsertSectorRequest
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public decimal Price { get; init; }
    public int? PeriodYear { get; init; }
    public int? PeriodMonth { get; init; }
}

public sealed record SectorQuery : BaseSearchObject
{
    public Guid? ProductId { get; init; }
    public PublishStatus? Status { get; init; }

    /// <summary>Which calendar date to report DailyEntry availability for. Optional, and ignored
    /// by every other TicketingMode — those count capacity per sector, not per day, so their
    /// RemainingCapacity is the same number whatever date is asked for.</summary>
    public DateOnly? Date { get; init; }
}

public record HoldSectorRequest
{
    public int Quantity { get; init; } = 1;

    // Required iff the sector's TicketingMode is DailyEntry — which exact calendar date within
    // the sector's PeriodYear/PeriodMonth the buyer wants tickets for.
    public DateOnly? Date { get; init; }
}

public record HoldSectorResponse(string HoldId, DateTime ExpiresAt);
