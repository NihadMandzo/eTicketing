using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Subscriptions;

/// <summary>
/// The webhook-driven half of subscriptions: what happens to a parking reservation months after
/// anyone was last on the site.
///
/// Every method here is keyed by the provider's subscription reference and is safe to run twice --
/// deliveries are at-least-once, and although eTicketing.Payment de-duplicates by provider event id,
/// this side must not depend on that alone.
/// </summary>
public interface ISubscriptionRenewalService
{
    Task RenewAsync(SubscriptionRenewed message, CancellationToken ct = default);

    Task MarkPastDueAsync(SubscriptionPaymentFailed message, CancellationToken ct = default);

    Task CancelAsync(SubscriptionCancelled message, CancellationToken ct = default);
}

public class SubscriptionRenewalService : ISubscriptionRenewalService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ISectorRepository _sectorRepository;
    private readonly IProductSnapshotRepository _productSnapshots;
    private readonly ISectorCapacityLock _capacityLock;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TicketQrCodec _qrCodec;
    private readonly PlatformClock _clock;
    private readonly ILogger<SubscriptionRenewalService> _logger;

    public SubscriptionRenewalService(
        ISubscriptionRepository subscriptionRepository,
        ITicketRepository ticketRepository,
        ISectorRepository sectorRepository,
        IProductSnapshotRepository productSnapshots,
        ISectorCapacityLock capacityLock,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        TicketQrCodec qrCodec,
        PlatformClock clock,
        ILogger<SubscriptionRenewalService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _ticketRepository = ticketRepository;
        _sectorRepository = sectorRepository;
        _productSnapshots = productSnapshots;
        _capacityLock = capacityLock;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _qrCodec = qrCodec;
        _clock = clock;
        _logger = logger;
    }

    public async Task RenewAsync(SubscriptionRenewed message, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByPaymentReferenceAsync(message.ProviderSubscriptionId, ct);
        if (subscription is null)
        {
            // Not an exception: an event for a subscription this database has never heard of (a
            // stale reference, or a subscription created against a different environment sharing the
            // provider account) would otherwise dead-letter-storm the consumer forever.
            _logger.LogWarning(
                "Obnova za nepoznatu pretplatu {Reference} -- ignorišem.", message.ProviderSubscriptionId);
            return;
        }

        // Idempotency guard independent of Payment's event de-duplication: if this exact period was
        // already minted, a redelivery must not produce a second ticket for a month paid once.
        var alreadyMinted = await _ticketRepository.ExistsForSubscriptionPeriodAsync(
            subscription.Id, message.PeriodStart, ct);

        if (alreadyMinted)
        {
            _logger.LogInformation(
                "Ulaznica za pretplatu {SubscriptionId} i period {PeriodStart} već postoji -- preskačem.",
                subscription.Id, message.PeriodStart);
            return;
        }

        var sector = await _sectorRepository.GetByIdAsync(subscription.SectorId, ct);
        if (sector is null)
        {
            _logger.LogError(
                "Pretplata {SubscriptionId} pokazuje na nepostojeći sektor {SectorId}.",
                subscription.Id, subscription.SectorId);
            return;
        }

        // Refused for an already-cancelled subscription, which matters because cancelling released
        // the parking space back to the sector: minting a ticket here would hand out a bay that has
        // already been resold. See Subscription.Renew.
        if (!subscription.Renew(message.PeriodStart, message.PeriodEnd))
        {
            _logger.LogWarning(
                "Obnova otkazane pretplate {SubscriptionId} -- ignorišem.", subscription.Id);
            return;
        }

        // No capacity call. The space was permanently decremented at first purchase and stays taken
        // for the life of the subscription -- re-holding it would fail, since remaining is zero.
        var orderId = Guid.NewGuid();
        var ticket = Ticket.ForRecurringReservation(
            sector.Id, ticketTypeId: null, orderId, sector.ProductId,
            subscription.UserId, subscription.UserEmail ?? string.Empty,
            message.AmountPaid, subscription.Id, message.PeriodStart, message.PeriodEnd);

        await _ticketRepository.AddAsync(ticket, ct);

        // Deliberately not a gate. The provider has already taken this month's money on its own
        // schedule; refusing to mint the ticket because the product was unpublished would leave a
        // paying subscriber with nothing. Absent snapshot just means the PDF falls back to a
        // generic heading — see TicketPurchased.ProductName.
        var product = await _productSnapshots.GetByIdNoTrackingAsync(sector.ProductId, ct);

        // Same event the synchronous purchase publishes, so the existing Notifications and
        // PdfGeneration consumers email the new period's ticket with no extra wiring — and
        // published before the save for the same reason PurchaseService does it: the outbox row
        // belongs in the same transaction as the ticket it announces. The timestamp comes from the
        // clock rather than ticket.CreatedAt, which the audit interceptor only fills in during
        // SaveChangesAsync.
        await _eventPublisher.PublishAsync(
            EventNames.TicketPurchased,
            new TicketPurchased(
                orderId, sector.ProductId, sector.Id, sector.Name, sector.TicketingMode,
                subscription.UserId, subscription.UserEmail ?? string.Empty,
                message.AmountPaid, _clock.UtcNow,
                [new PurchasedTicket(ticket.Id, _qrCodec.Sign(ticket.Id), null, ticket.PricePaid,
                    ticket.ValidDate, ticket.ValidFrom, ticket.ValidTo)],
                product?.Name, product?.Date, product?.City),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Obnovljena pretplata {SubscriptionId} za period {PeriodStart} - {PeriodEnd}.",
            subscription.Id, message.PeriodStart, message.PeriodEnd);
    }

    public async Task MarkPastDueAsync(SubscriptionPaymentFailed message, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByPaymentReferenceAsync(message.ProviderSubscriptionId, ct);
        if (subscription is null)
        {
            _logger.LogWarning(
                "Neuspjela naplata za nepoznatu pretplatu {Reference} -- ignorišem.", message.ProviderSubscriptionId);
            return;
        }

        // Nothing is released here. The provider is still working through its own dunning retries
        // and the buyer keeps the space while it does; only an actual cancellation frees it. False
        // means it was already dead — a late failure notice must not resurrect it into PastDue.
        if (!subscription.MarkPastDue())
            return;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Pretplata {SubscriptionId} je neplaćena ({Reason}).", subscription.Id, message.Reason);
    }

    public async Task CancelAsync(SubscriptionCancelled message, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByPaymentReferenceAsync(message.ProviderSubscriptionId, ct);
        if (subscription is null)
        {
            _logger.LogWarning(
                "Otkazivanje nepoznate pretplate {Reference} -- ignorišem.", message.ProviderSubscriptionId);
            return;
        }

        // False means a redelivery of a cancellation already processed. Returning here is not just
        // tidiness — it is what stops the capacity release below running twice and handing the same
        // space back to the sector's counter two or three times over.
        if (!subscription.Cancel(message.CancelledAt))
            return;

        await _unitOfWork.SaveChangesAsync(ct);

        // THE point of this handler. The space was permanently decremented at first purchase, so
        // without this the parking spot stays sold forever and nobody else can ever reserve it.
        // ReleaseConfirmedAsync rather than ReleaseAsync because the hold was confirmed months ago
        // and its holdinfo pointer is long gone -- which is exactly why CapacityHoldId is stored.
        if (!string.IsNullOrWhiteSpace(subscription.CapacityHoldId))
        {
            await _capacityLock.ReleaseConfirmedAsync(
                subscription.SectorId, date: null, subscription.CapacityHoldId, ct);
        }
        else
        {
            // Only possible for a subscription created before CapacityHoldId existed. Say so, because
            // the space needs freeing by hand.
            _logger.LogError(
                "Pretplata {SubscriptionId} nema CapacityHoldId -- kapacitet sektora {SectorId} mora biti oslobođen ručno.",
                subscription.Id, subscription.SectorId);
        }

        _logger.LogInformation("Pretplata {SubscriptionId} je otkazana.", subscription.Id);
    }
}
