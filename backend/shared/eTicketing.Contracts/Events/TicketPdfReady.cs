namespace eTicketing.Contracts.Events;

/// <summary>
/// Published by eTicketing.PdfGeneration once every ticket PDF in an order is uploaded to blob
/// storage. Two independent consumers:
///  - eTicketing.Notifications sends one confirmation email with every <see cref="TicketPdf"/>
///    attached (Notifications reads each blob by BlobName and sends the bytes inline).
///  - eTicketing.Ticketing stamps Ticket.PdfBlobName and flips Status Confirmed → Ready.
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

public record TicketPdf(
    Guid TicketId,
    string BlobName,
    string FileName,
    string SectorName,
    string? TicketTypeName,
    decimal PricePaid);
