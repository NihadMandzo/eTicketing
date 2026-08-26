using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>
/// Reads and writes for the physical-ticket export batches. The rendered PDF lives in a separate
/// <see cref="TicketPrintBatchFile"/> row, so everything here except <see cref="GetFileAsync"/> and
/// <see cref="GetFileRowAsync"/> stays cheap no matter how large the sheet is.
/// </summary>
public interface ITicketPrintBatchRepository : IRepository<TicketPrintBatch, Guid>
{
    /// <summary>Untracked header for read-only paths (polling, ownership checks).</summary>
    Task<TicketPrintBatch?> GetHeaderAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every batch belonging to this organization that still has something to show the
    /// organizer: rendering in progress, a file waiting to be downloaded, or a failure to retry.
    /// Backs the desktop's top-bar "your PDF is ready" badge.</summary>
    Task<List<TicketPrintBatch>> GetOutstandingForOrganizationAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>The most recent batch for a product, so re-opening the export screen picks up a
    /// render that was started before the organizer navigated away.</summary>
    Task<TicketPrintBatch?> GetLatestForProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>True while a batch for this product is queued or rendering. Only one may be in
    /// flight at a time, so the export screen never has to reason about two overlapping jobs.</summary>
    Task<bool> HasInFlightForProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Ids of every batch left Queued or Rendering, oldest first — what the render worker
    /// re-enqueues on startup so a restart mid-render resumes instead of stranding an organizer on
    /// a spinner forever.</summary>
    Task<List<Guid>> GetUnfinishedIdsAsync(CancellationToken ct = default);

    /// <summary>Ids of Ready batches whose file was never collected and is now older than the
    /// cutoff — the sweep that stops the table growing without bound.</summary>
    Task<List<Guid>> GetStaleReadyIdsAsync(DateTime completedBefore, CancellationToken ct = default);

    /// <summary>The rendered bytes on their own, untracked. Null when the batch is unknown or its
    /// file has already been handed over or swept away.</summary>
    Task<byte[]?> GetFileAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>The file row, tracked, so it can be removed once the bytes are handed over. Loads
    /// the content along with it — only ever call this on a path that is about to delete it.</summary>
    Task<TicketPrintBatchFile?> GetFileRowAsync(Guid batchId, CancellationToken ct = default);

    void AddFile(TicketPrintBatchFile file);
    void RemoveFile(TicketPrintBatchFile file);
}
