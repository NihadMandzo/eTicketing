namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>Query for GET /ticket-print-batches/latest — see <see cref="TicketPrintOptionsQuery"/>
/// for why this is a bound record rather than a loose Guid parameter.</summary>
public sealed record TicketPrintLatestQuery
{
    public Guid ProductId { get; init; }
}
