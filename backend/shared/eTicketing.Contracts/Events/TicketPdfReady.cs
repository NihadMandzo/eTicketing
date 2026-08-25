namespace eTicketing.Contracts.Events;

/// <summary>
/// Published by eTicketing.PdfGeneration once every ticket PDF in an order has been rendered. Two
/// independent consumers:
///  - eTicketing.Notifications sends one confirmation email with every <see cref="TicketPdf"/>
///    attached.
///  - eTicketing.Ticketing flips Status Confirmed → Ready.
/// This is the "publish a return event rather than call back into Ticketing over HTTP" half of
/// SPRINT_4 T-4.2.4's explicit choice.
/// </summary>
public record TicketPdfReady(
    Guid OrderId,
    Guid ProductId,
    Guid UserId,
    string UserEmail,
    string ProductName,
    DateTime? ProductDate,
    string ProductCity,
    decimal TotalPaid,
    IReadOnlyList<TicketPdf> Tickets);

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
