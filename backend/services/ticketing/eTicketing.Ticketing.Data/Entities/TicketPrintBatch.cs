using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// One bulk export of physical, printable tickets for a Product, requested by an organizer from
/// the desktop back-office. Creating the batch is what mints the Tickets and claims their capacity
/// — the PDF is only paper. Rendering happens afterwards, off the request thread, in
/// TicketPrintRenderWorker, because a batch can run to thousands of tickets.
///
/// <para>This row stays small on purpose: the rendered PDF lives in a separate
/// <see cref="TicketPrintBatchFile"/> row, so listing batches, polling status and flipping state
/// never drag tens of megabytes through the change tracker.</para>
/// </summary>
public class TicketPrintBatch : BaseEntity
{
    public Guid Id { get; set; }

    // Cross-service ref to Catalog.Product — plain Guid column, same convention as Sector.
    public Guid ProductId { get; set; }

    // Denormalized from the product at request time so ownership checks on later polls/downloads
    // never need another cross-service call.
    public Guid OrganizationId { get; set; }
    public Guid RequestedByUserId { get; set; }

    // Denormalized product name, so the desktop's "ready for download" badge can label the batch
    // without fanning out to Catalog for every row it shows.
    public string ProductName { get; set; } = string.Empty;

    public TicketPrintBatchStatus Status { get; set; } = TicketPrintBatchStatus.Queued;

    public int TicketCount { get; set; }

    // Advances while the worker renders, so the UI can show real progress rather than a spinner
    // with no end in sight.
    public int RenderedCount { get; set; }

    // ceil(TicketCount / PrintSheetDocument.TicketsPerSheet). Written once the render finishes.
    public int PageCount { get; set; }

    // The inclusive range of human-readable stub numbers this batch printed. Serial numbers run
    // per product and never restart, so a later batch continues where this one stopped.
    public int SerialFrom { get; set; }
    public int SerialTo { get; set; }

    // Sum of the face value of every ticket in the batch — what the organizer is accountable for
    // once the paper leaves the printer.
    public decimal NominalValue { get; set; }

    // DailyEntry batches only: which calendar day every ticket in the batch admits entry for.
    public DateOnly? ValidDate { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DownloadedAt { get; set; }

    /// <summary>
    /// When the organizer cleared this batch from their notification badge.
    /// <see cref="ITicketPrintBatchRepository.GetOutstandingForOrganizationAsync"/> hides stamped
    /// rows, exactly as it already hides collected ones via <see cref="DownloadedAt"/>.
    ///
    /// <para><b>Not a soft delete</b> (which this codebase has nowhere, deliberately) — the batch
    /// row is the durable record of a print run: which serial numbers went to paper, and what
    /// face value the organizer is accountable for. Hard-deleting it to clear a notification
    /// would destroy that. This is the same shape as <see cref="DownloadedAt"/>: a timestamp
    /// saying the badge is done with the row, not that the row is gone.</para>
    ///
    /// <para>It exists because before it there was no way to clear a notification at all. A row
    /// left the badge only as a side effect of a completed download, so a Failed batch nobody
    /// could render, or a Ready one whose file the sweep had already deleted, sat there
    /// permanently with no action that would remove it.</para>
    /// </summary>
    public DateTime? DismissedAt { get; set; }
}
