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

public sealed record TicketPrintSectorOption(
    Guid SectorId,
    string Name,
    int Capacity,
    int Remaining,
    decimal Price,
    int? PeriodYear,
    int? PeriodMonth,
    IReadOnlyList<TicketPrintTicketTypeOption> TicketTypes);

public sealed record TicketPrintTicketTypeOption(Guid Id, string Name, decimal Price);

/// <summary>One request line: how many tickets of one price tier in one sector. A sector with no
/// TicketTypes is addressed with a null <see cref="TicketTypeId"/> and priced at Sector.Price,
/// mirroring how PurchaseService treats the same case.</summary>
public sealed record TicketPrintLineRequest
{
    public Guid SectorId { get; init; }
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}

public sealed record CreateTicketPrintBatchRequest
{
    public Guid ProductId { get; init; }

    /// <summary>Required for a DailyEntry product and rejected for any other: which calendar day
    /// every ticket in the batch admits entry for. DailyEntry capacity is tracked per (sector,
    /// date), so there is no such thing as a day-pass batch without a day.</summary>
    public DateOnly? ValidDate { get; init; }

    public IReadOnlyList<TicketPrintLineRequest> Lines { get; init; } = [];
}

/// <summary>The batch as the desktop sees it. Carries no file bytes — the download is a separate
/// call, and it is the only thing that ever moves the PDF.</summary>
public sealed record TicketPrintBatchResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    TicketPrintBatchStatus Status,
    int TicketCount,
    int RenderedCount,
    int PageCount,
    int SerialFrom,
    int SerialTo,
    decimal NominalValue,
    DateOnly? ValidDate,
    long? FileSizeBytes,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? DownloadedAt);

/// <summary>A rendered batch, ready to be streamed to the organizer exactly once.</summary>
public sealed record TicketPrintDownload(byte[] Content, string FileName);
