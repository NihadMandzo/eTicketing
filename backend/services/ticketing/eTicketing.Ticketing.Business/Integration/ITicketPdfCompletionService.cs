using eTicketing.Contracts.Events;

namespace eTicketing.Ticketing.Business.Integration;

public interface ITicketPdfCompletionService
{
    /// <summary>Applies a <see cref="TicketPdfReady"/> to the tickets it names, moving each
    /// Confirmed → Ready. Idempotent — a redelivered event re-writes the same values.</summary>
    Task ApplyAsync(TicketPdfReady message, CancellationToken ct = default);
}
