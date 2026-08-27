namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>
/// Hand-off from the request thread to the render worker.
///
/// This is deliberately an in-process queue rather than a RabbitMQ topic. Every other asynchronous
/// flow in this platform crosses a service boundary — a purchase in Ticketing has to reach
/// Notifications and PdfGeneration — and RabbitMQ is what carries it there. Rendering a print batch
/// crosses nothing: the tickets, the QR signing key and the finished file all live inside
/// eTicketing.Ticketing, so a broker round-trip would buy latency and an extra failure mode and
/// nothing else.
///
/// The durable queue is the TicketPrintBatch table itself. This channel is only a nudge, so that a
/// batch starts rendering immediately instead of waiting for the next sweep; anything left behind
/// by a restart is picked back up from the table on startup (see TicketPrintRenderWorker).
/// Implementation lives in .Api/Infrastructure — hosting concern, per .claude/rules/10-backend.md.
/// </summary>
public interface ITicketPrintQueue
{
    /// <summary>Nudge the worker to pick up this batch. Must be called after the batch row is
    /// committed, never before — the worker reads it straight back out of the database.</summary>
    void Enqueue(Guid batchId);
}
