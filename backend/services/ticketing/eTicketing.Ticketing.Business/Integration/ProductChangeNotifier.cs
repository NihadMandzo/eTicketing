using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Integration;

/// <summary>
/// The hop that makes "everyone who bought a ticket hears about a change" possible at all.
///
/// eTicketing.Catalog owns Product and knows exactly what changed, but has no idea who bought
/// anything. eTicketing.Ticketing owns Ticket and knows every buyer, but never sees an edit. So
/// Catalog publishes ProductUpdated, this class turns it into one ProductChangedNotification per
/// distinct recipient, and eTicketing.Notifications — which stays a pure email renderer with no
/// clients of its own — sends them.
///
/// Recipients are deduplicated by the repository (a buyer with three tickets to the same show gets
/// one email) and filtered to tickets that can still be used, so nobody is emailed about a change
/// to something they already attended or that was cancelled.
/// </summary>
public class ProductChangeNotifier : IProductChangeNotifier
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformClock _clock;
    private readonly ILogger<ProductChangeNotifier> _logger;

    public ProductChangeNotifier(
        ITicketRepository ticketRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        PlatformClock clock,
        ILogger<ProductChangeNotifier> logger)
    {
        _ticketRepository = ticketRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyBuyersAsync(ProductUpdated message, CancellationToken ct = default)
    {
        // Catalog only publishes when something actually changed on a published product, but an
        // empty list would mean mailing people a change notice listing no changes — guard anyway.
        if (message.Changes.Count == 0)
            return;

        // Local business day, not UTC — see PlatformClock. A buyer whose ticket is valid "today"
        // locally must not be filtered out because UTC is still on yesterday.
        var today = _clock.Today();
        var buyers = await _ticketRepository.GetLiveBuyersForProductAsync(message.ProductId, today, ct);

        if (buyers.Count == 0)
        {
            _logger.LogInformation(
                "Proizvod {ProductId} je izmijenjen, ali nema aktivnih kupaca — nema koga obavijestiti.", message.ProductId);
            return;
        }

        foreach (var buyer in buyers)
        {
            await _eventPublisher.PublishAsync(
                EventNames.ProductChanged,
                new ProductChangedNotification(message.ProductId, message.ProductName, buyer.UserEmail, message.Changes),
                ct);
        }

        // These publishes write outbox rows rather than reaching the broker, and a row that is
        // never saved is an event that silently never happens. This class has no domain write of
        // its own — it is a pure fan-out — so the save that commits them has to be explicit. One
        // call for the whole fan-out, so a recipient list either goes out entirely or not at all.
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Proizvod {ProductId} je izmijenjen — obavještenje poslano za {Count} kupaca.", message.ProductId, buyers.Count);
    }
}
