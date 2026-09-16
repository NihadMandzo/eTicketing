namespace eTicketing.Ticketing.Business.Sectors;

public record HoldSectorRequest
{
    public int Quantity { get; init; } = 1;

    // Required iff the sector's TicketingMode is DailyEntry — which exact calendar date within
    // the sector's PeriodYear/PeriodMonth the buyer wants tickets for.
    public DateOnly? Date { get; init; }
}
