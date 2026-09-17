using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.TicketPrint;

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
