using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.Sectors;

public sealed record SectorQuery : BaseSearchObject
{
    public Guid? ProductId { get; init; }
    public PublishStatus? Status { get; init; }

    /// <summary>Which calendar date to report DailyEntry availability for. Optional, and ignored
    /// by every other TicketingMode — those count capacity per sector, not per day, so their
    /// RemainingCapacity is the same number whatever date is asked for.</summary>
    public DateOnly? Date { get; init; }
}
