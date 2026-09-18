namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>Query for GET /ticket-print-batches/options. Bound with [AsParameters] so the ids it
/// carries go through a validator — the route previously took a bare Guid parameter, which meant
/// Guid.Empty bound happily and reached a cross-service HTTP call before anything rejected it.
/// Property names match the existing query-string keys exactly (productId, date); renaming either
/// would silently break the desktop client.</summary>
public sealed record TicketPrintOptionsQuery
{
    public Guid ProductId { get; init; }
    public DateOnly? Date { get; init; }
}
