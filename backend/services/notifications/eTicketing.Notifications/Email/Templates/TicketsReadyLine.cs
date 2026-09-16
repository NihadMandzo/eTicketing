namespace eTicketing.Notifications.Email.Templates;

/// <summary>One row per ticket in the order — the PDFs themselves ride as attachments (see
/// EmailAttachment), this table is just so the buyer can tell at a glance what they got without
/// opening three files.</summary>
public sealed record TicketsReadyLine(string TicketCode, string SectorName, string? TicketTypeName, decimal PricePaid);
