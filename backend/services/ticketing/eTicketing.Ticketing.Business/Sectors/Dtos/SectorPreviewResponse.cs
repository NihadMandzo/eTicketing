using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.Sectors;

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
