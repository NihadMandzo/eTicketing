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
