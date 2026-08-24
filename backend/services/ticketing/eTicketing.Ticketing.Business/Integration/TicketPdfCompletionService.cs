using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Integration;

/// <summary>
/// Closes the loop SPRINT_4 T-4.2.4 opened: eTicketing.PdfGeneration finishes an order's PDFs and
/// publishes TicketPdfReady rather than calling back into this service over HTTP, and this is where
/// that event lands. Stamps each Ticket's PdfBlobName (which is what makes TicketResponse.PdfUrl
/// stop being null) and moves it Confirmed → Ready.
///
/// Deliberately forgiving in both directions, because this runs off a broker that guarantees
/// at-least-once, not exactly-once:
///  - Unknown ticket ids are skipped, not thrown on. A poison-queue trip helps nobody if the ticket
///    was legitimately deleted between purchase and PDF completion.
///  - Re-applying the same event is a no-op that rewrites identical values.
///  - A ticket already Used (someone walked in before the PDF finished — unlikely but possible for
///    a same-day purchase at the door) keeps its terminal status; only the blob name is recorded.
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

        var blobNamesByTicketId = message.Tickets
            // A malformed event naming the same ticket twice would otherwise throw out of
            // ToDictionary and dead-letter the whole order.
            .GroupBy(t => t.TicketId)
            .ToDictionary(g => g.Key, g => g.First().BlobName);

        var tickets = await _ticketRepository.GetByIdsAsync(blobNamesByTicketId.Keys.ToList(), ct);

        var missing = blobNamesByTicketId.Count - tickets.Count;
        if (missing > 0)
        {
            _logger.LogWarning(
                "TicketPdfReady za narudžbu {OrderId} referencira {MissingCount} ulaznica koje više ne postoje — preskačem ih.",
                message.OrderId, missing);
        }

        foreach (var ticket in tickets)
        {
            ticket.PdfBlobName = blobNamesByTicketId[ticket.Id];

            if (ticket.Status == TicketStatus.Confirmed)
                ticket.Status = TicketStatus.Ready;
        }

        if (tickets.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PDF ulaznice za narudžbu {OrderId} su spremne — ažurirano {Count} ulaznica.", message.OrderId, tickets.Count);
    }
}
