using eTicketing.Contracts.Events;
using eTicketing.Ticketing.Business.Purchases;
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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProductChangeNotifier> _logger;

    public ProductChangeNotifier(
        ITicketRepository ticketRepository,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider,
        ILogger<ProductChangeNotifier> logger)
    {
        _ticketRepository = ticketRepository;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task NotifyBuyersAsync(ProductUpdated message, CancellationToken ct = default)
    {
        // Catalog only publishes when something actually changed on a published product, but an
        // empty list would mean mailing people a change notice listing no changes — guard anyway.
        if (message.Changes.Count == 0)
            return;

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
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

        _logger.LogInformation(
            "Proizvod {ProductId} je izmijenjen — obavještenje poslano za {Count} kupaca.", message.ProductId, buyers.Count);
    }
}
