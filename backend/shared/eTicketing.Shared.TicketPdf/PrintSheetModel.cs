using eTicketing.Contracts.Persistence;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// A whole print batch as the renderer sees it: the product every ticket belongs to, plus the
/// tickets themselves in stub-number order. <see cref="PrintSheetDocument"/> lays these out three
/// to an A4 sheet.
/// </summary>
public sealed record PrintSheetModel(
    string ProductName,
    string ProductCity,
    DateTime? ProductDate,
    TicketingMode TicketingMode,
    DateTime IssuedAt,
    TicketSupportInfo Support,
    IReadOnlyList<PrintTicketModel> Tickets);
