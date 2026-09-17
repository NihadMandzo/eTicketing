namespace eTicketing.Contracts.Events;

/// <summary>
/// One rendered ticket, carried by value.
///
/// <paramref name="Content"/> is the PDF itself (base64 on the wire), not a pointer to it. Ticket
/// PDFs are deliberately never persisted: they are a pure function of the ticket, so storing them
/// would mean keeping a copy of a gate-opening QR code around for no reason. The trade-off is
/// message size — roughly 130 KB per ticket — which is why the buyer-facing download regenerates
/// the sheet on demand (GET /tickets/{id}/pdf) rather than being served from anywhere.
/// </summary>
public record TicketPdf(
    Guid TicketId,
    byte[] Content,
    string FileName,
    string SectorName,
    string? TicketTypeName,
    decimal PricePaid);
