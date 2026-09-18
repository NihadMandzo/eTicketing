namespace eTicketing.Ticketing.Data.Entities;

/// <summary>Persisted as the integer ordinal and mirrored by ordinal in the Flutter desktop's
/// TicketPrintBatchStatus. Append new values only; never reorder or remove.</summary>
public enum TicketPrintBatchStatus
{
    Queued,     // Tickets minted and capacity claimed; waiting for the render worker
    Rendering,  // TicketPrintRenderWorker has picked it up
    Ready,      // The file row holds the finished PDF, waiting to be downloaded once
    Failed,     // Rendering blew up; ErrorMessage says why, and the batch can be retried
    Expired     // Nobody collected the PDF and the retention sweep deleted it; see below
}
