using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>Everything the desktop export screen needs to draw itself: which sectors and price
/// tiers exist, how much room each still has, and where stub numbering will pick up. Fetched once
/// when the screen opens and again after a download, so "preostalo" reflects what the batch just
/// consumed.</summary>
public sealed record TicketPrintOptionsResponse(
    Guid ProductId,
    string ProductName,
    TicketingMode TicketingMode,
    DateTime? ProductDate,
    bool CanExport,
    string? BlockedReason,
    int NextSerialNumber,
    int MaxTicketsPerBatch,
    int TicketsPerSheet,
    IReadOnlyList<TicketPrintSectorOption> Sectors);
