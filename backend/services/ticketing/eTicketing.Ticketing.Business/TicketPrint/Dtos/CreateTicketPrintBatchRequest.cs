namespace eTicketing.Ticketing.Business.TicketPrint;

public sealed record CreateTicketPrintBatchRequest
{
    public Guid ProductId { get; init; }

    /// <summary>Required for a DailyEntry product and rejected for any other: which calendar day
    /// every ticket in the batch admits entry for. DailyEntry capacity is tracked per (sector,
    /// date), so there is no such thing as a day-pass batch without a day.</summary>
    public DateOnly? ValidDate { get; init; }

    public IReadOnlyList<TicketPrintLineRequest> Lines { get; init; } = [];
}
