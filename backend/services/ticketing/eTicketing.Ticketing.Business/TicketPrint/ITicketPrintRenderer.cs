namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>Renders one queued batch to a PDF and records the outcome. Driven by
/// TicketPrintRenderWorker in .Api — kept here, in Business, because it is domain work rather than
/// hosting plumbing, and because that makes it testable without a host.</summary>
public interface ITicketPrintRenderer
{
    /// <summary>Renders the batch and moves it to Ready, or to Failed with a Bosnian reason. Never
    /// throws for an expected failure: the worker must not be able to die on one bad batch, and the
    /// export screen must never be left waiting on a spinner that will not end.</summary>
    Task RenderAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Puts every batch left mid-render by a restart back in the queue, and returns the
    /// ids so the caller can nudge them. Also picks up anything still Queued.</summary>
    Task<List<Guid>> RecoverUnfinishedAsync(CancellationToken ct = default);

    /// <summary>Destroys stored files nobody ever collected. See TicketPrintBatchFile's remarks for
    /// why they must not linger.</summary>
    Task<int> SweepStaleFilesAsync(TimeSpan retention, CancellationToken ct = default);
}
