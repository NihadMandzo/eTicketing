using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Integration;

/// <summary>
/// Closes the loop SPRINT_4 T-4.2.4 opened: eTicketing.PdfGeneration finishes an order's PDFs and
/// publishes TicketPdfReady rather than calling back into this service over HTTP, and this is where
/// that event lands. Moves each ticket Confirmed → Ready.
///
/// "Ready" means the buyer has been sent their ticket, not that a file exists somewhere: PDFs are
/// rendered on demand and never stored (see TicketPdfService).
///
/// Deliberately forgiving in both directions, because this runs off a broker that guarantees
/// at-least-once, not exactly-once:
///  - Unknown ticket ids are skipped, not thrown on. A poison-queue trip helps nobody if the ticket
///    was legitimately deleted between purchase and PDF completion.
///  - Re-applying the same event is a no-op that rewrites identical values.
///  - A ticket already Used (someone walked in before the PDF finished — unlikely but possible for
///    a same-day purchase at the door) keeps its terminal status.
/// </summary>
public class TicketPdfCompletionService : ITicketPdfCompletionService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TicketPdfCompletionService> _logger;

    public TicketPdfCompletionService(
        ITicketRepository ticketRepository, IUnitOfWork unitOfWork, ILogger<TicketPdfCompletionService> logger)
    {
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ApplyAsync(TicketPdfReady message, CancellationToken ct = default)
    {
        if (message.Tickets.Count == 0)
            return;

        // Distinct because a malformed event naming the same ticket twice should not be able to
        // skew the "how many are missing" count below.
        var ticketIds = message.Tickets.Select(t => t.TicketId).Distinct().ToList();

        var tickets = await _ticketRepository.GetByIdsAsync(ticketIds, ct);

        var missing = ticketIds.Count - tickets.Count;
        if (missing > 0)
        {
            _logger.LogWarning(
                "TicketPdfReady za narudžbu {OrderId} referencira {MissingCount} ulaznica koje više ne postoje — preskačem ih.",
                message.OrderId, missing);
        }

        foreach (var ticket in tickets.Where(t => t.Status == TicketStatus.Confirmed))
        {
            ticket.Status = TicketStatus.Ready;
        }

        if (tickets.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PDF ulaznice za narudžbu {OrderId} su spremne — ažurirano {Count} ulaznica.", message.OrderId, tickets.Count);
    }
}
