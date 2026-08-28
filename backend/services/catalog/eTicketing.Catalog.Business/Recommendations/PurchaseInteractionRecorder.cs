using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using Microsoft.Extensions.Logging;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>
/// Turns a <see cref="TicketPurchased"/> off the bus into the strongest signal this service
/// collects. Deliberately a plain business class rather than logic inside the RabbitMQ consumer:
/// the messaging shell in .Api does nothing but deserialize and call this, so the actual behavior
/// is unit-testable without a broker.
///
/// The event already carries UserId and ProductId, so nothing has to be looked up in Ticketing.
/// </summary>
public class PurchaseInteractionRecorder
{
    private readonly IUserInteractionRepository _interactions;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PurchaseInteractionRecorder> _logger;

    public PurchaseInteractionRecorder(
        IUserInteractionRepository interactions,
        IProductRepository products,
        IUnitOfWork unitOfWork,
        ILogger<PurchaseInteractionRecorder> logger)
    {
        _interactions = interactions;
        _products = products;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Idempotent by construction, which is what makes an at-least-once broker safe here: the
    /// upsert is keyed on (UserId, ProductId, Type), so a redelivered event bumps a counter rather
    /// than inserting a duplicate. One order minting three tickets is likewise one purchase signal,
    /// not three — the event is published per order, and interest in a product doesn't triple
    /// because somebody brought friends.
    /// </summary>
    public async Task RecordAsync(TicketPurchased purchase, CancellationToken ct = default)
    {
        // Catalog is not the system of record for purchases, so a product it doesn't know about
        // (deleted between purchase and delivery) is a skip, not a failure — failing would send the
        // message around the retry loop forever over a row that will never exist again.
        var product = await _products.GetByIdAsync(purchase.ProductId, ct);
        if (product is null)
        {
            _logger.LogWarning(
                "Preskačem interakciju za nepoznat proizvod {ProductId} iz narudžbe {OrderId}.",
                purchase.ProductId, purchase.OrderId);
            return;
        }

        await _interactions.UpsertAsync(
            purchase.UserId, purchase.ProductId, InteractionType.Purchase, purchase.PurchasedAt, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
