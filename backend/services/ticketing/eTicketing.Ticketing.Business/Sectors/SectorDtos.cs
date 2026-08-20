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
    IReadOnlyList<TicketTypeResponse> TicketTypes);

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
}

public record HoldSectorRequest
{
    public int Quantity { get; init; } = 1;

    // Required iff the sector's TicketingMode is DailyEntry — which exact calendar date within
    // the sector's PeriodYear/PeriodMonth the buyer wants tickets for.
    public DateOnly? Date { get; init; }
}

public record HoldSectorResponse(string HoldId, DateTime ExpiresAt);
