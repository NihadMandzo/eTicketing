using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Purchases;

/// <summary>What a hold actually entitles the caller to buy, resolved server-side. Every field here
/// comes from Redis and the database, never from the request.</summary>
/// <param name="Product">The local read model of the eTicketing.Catalog product this sector belongs
/// to. Resolved once here because both jobs need it: refusing a purchase for a product that is no
/// longer published, and stamping the ticket's event name/date/city onto TicketPurchased so
/// eTicketing.PdfGeneration needs no catalogue client of its own.</param>
public record ResolvedOrder(
    HeldReservation Reservation,
    Sector Sector,
    ProductSnapshot Product,
    IReadOnlyDictionary<Guid, TicketType> TicketTypesById,
    decimal TotalPrice);

/// <summary>
/// Orchestrates the purchase critical path from .claude/rules/01-domain.md, in two calls.
///
/// POST /purchases/payment-intent prices the hold and creates the provider-side payment object; the
/// buyer confirms it in their browser; POST /purchases then captures it and mints the tickets.
/// Splitting it this way is what keeps card data out of this service entirely — the card goes
/// straight from the browser to the provider, and only identifiers come back.
///
/// Both calls begin with the same ResolveOrderAsync prelude:
///  1. Resolve the hold's actual reservation from Redis via ISectorCapacityLock.PeekAsync — never
///     trust a client-supplied SectorId/quantity (see PeekAsync's own doc comment for why), and
///     refuse a hold owned by a different account. Also the guard against a replayed HoldId:
///     PeekAsync returns null once ConfirmAsync has already run for it (see
///     RedisSectorCapacityLock.ConfirmAsync).
///  2. Validate the request's line-item quantities sum to exactly what was held.
///  3. Load the held Sector (with TicketTypes) — must still exist and be Published.
///  4. Validate each line's TicketTypeId against the Sector's TicketTypes (all-or-nothing: either
///     every line needs one, or none do — the request-shape half of that rule lives in
///     PurchaseRequestValidator, the DB-dependent half lives here).
///  5. Compute the total price.
///
/// PurchaseAsync then continues:
///  6. Captures via IPaymentClient. One-time payments are authorized with manual capture, so until
///     this succeeds the buyer's money is only ring-fenced, never taken — which is what makes step 1
///     failing here recoverable: the authorization is cancelled and nothing moved. A
///     circuit-open/timeout/transport failure releases the hold and returns a 503 (Error.Failure);
///     this is a service outage, not the buyer's fault. eTicketing.Payment is itself idempotent on
///     OrderRef, so a retried capture after a lost response never double-charges.
///  7. A declined card releases the hold and returns 400 (Error.Validation) — the buyer's problem,
///     not an outage; deliberately different HTTP semantics from step 6.
///  8. Success confirms the hold (permanent capacity decrement).
///  9. Mints one Ticket per admission unit (not one Ticket with a Quantity field — each unit needs
///     its own QR/receipt identity, and TicketPurchased has no Quantity field to carry one anyway)
///     via the mode-specific Ticket.ForXxx factory. RecurringReservation additionally creates one
///     Subscription shared by that single Ticket (Capacity is always 1 for that mode), stamped with
///     the billing period the provider settled on rather than one computed here.
/// 10. Persists everything in one SaveChangesAsync, logged on failure — by this point the charge has
///     already succeeded, so a persistence failure here is silent money without a paper trail unless
///     it's logged loudly.
/// 11. Publishes ONE TicketPurchased for the whole order (not one per Ticket): eTicketing.PdfGeneration
///     renders a PDF per ticket but eTicketing.Notifications sends a single confirmation email
///     carrying all of them, which it can only do if it sees the order as one unit. Each line
///     carries the ticket's signed QR payload, minted here — Ticketing is the only service that
///     holds the QR signing key.
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly ISectorRepository _sectorRepository;
    private readonly IProductSnapshotRepository _productSnapshots;
    private readonly ITicketRepository _ticketRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISectorCapacityLock _capacityLock;
    private readonly IPaymentClient _paymentClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TicketQrCodec _qrCodec;
    private readonly TicketResponseFactory _responseFactory;
    private readonly PlatformClock _clock;
    private readonly ILogger<PurchaseService> _logger;

    public PurchaseService(
        ISectorRepository sectorRepository,
        IProductSnapshotRepository productSnapshots,
        ITicketRepository ticketRepository,
        ISubscriptionRepository subscriptionRepository,
        ISectorCapacityLock capacityLock,
        IPaymentClient paymentClient,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        TicketQrCodec qrCodec,
        TicketResponseFactory responseFactory,
        PlatformClock clock,
        ILogger<PurchaseService> logger)
    {
        _sectorRepository = sectorRepository;
        _productSnapshots = productSnapshots;
        _ticketRepository = ticketRepository;
        _subscriptionRepository = subscriptionRepository;
        _capacityLock = capacityLock;
        _paymentClient = paymentClient;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _qrCodec = qrCodec;
        _responseFactory = responseFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Steps 1-5, shared verbatim by both calls. Every error code and message here is the one
    /// callers and tests already depend on — this method was extracted from PurchaseAsync, not
    /// rewritten, precisely so those stayed identical.
    /// </summary>
    private async Task<Result<ResolvedOrder>> ResolveOrderAsync(
        string holdId, IReadOnlyList<PurchaseLineItemRequest> lineItems, Guid callerId, CancellationToken ct)
    {
        var reservation = await _capacityLock.PeekAsync(holdId, ct);

        // A hold belongs to the account that took it: spending someone else's is the same theft as
        // releasing it (see SectorService.ReleaseHoldAsync), except the thief walks away with the
        // seat. Deliberately the *same* error as an expired hold rather than a distinct "not
        // yours" — a different answer would confirm to whoever presented the id that it is live.
        // OwnerId is null for a hold with no owner: a system hold, or one minted before the owner
        // was recorded, which stays spendable for the rest of its five-minute TTL.
        if (reservation is null || (reservation.OwnerId is not null && reservation.OwnerId != callerId))
            return Result<ResolvedOrder>.Failure(Error.Validation("purchase.hold_expired", "Rezervacija je istekla ili ne postoji. Pokušajte ponovo."));

        var requestedQuantity = lineItems.Sum(li => li.Quantity);
        if (requestedQuantity != reservation.Quantity)
            return Result<ResolvedOrder>.Failure(Error.Validation("purchase.quantity_mismatch", "Količina se ne poklapa sa rezervacijom."));

        var sector = await _sectorRepository.GetByIdWithTicketTypesAsync(reservation.SectorId, ct);
        if (sector is null || sector.Status != PublishStatus.Published)
            return Result<ResolvedOrder>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        // Re-checked here and not only at hold time: a hold lives five minutes, and the product can
        // be unpublished or deleted inside that window. Held capacity is not a right to buy
        // something that has since been taken off sale. Read from the local snapshot — the purchase
        // critical path gets no new synchronous dependency.
        var product = await _productSnapshots.GetByIdNoTrackingAsync(sector.ProductId, ct);
        if (product is null || product.Status != PublishStatus.Published)
            return Result<ResolvedOrder>.Failure(SectorService.ProductUnavailable());

        var ticketTypesById = sector.TicketTypes.ToDictionary(t => t.Id);
        if (ticketTypesById.Count > 0)
        {
            if (lineItems.Any(li => li.TicketTypeId is null || !ticketTypesById.ContainsKey(li.TicketTypeId.Value)))
                return Result<ResolvedOrder>.Failure(Error.Validation("purchase.invalid_ticket_type", "Odabrani tip ulaznice ne pripada ovom sektoru."));
        }
        else if (lineItems.Any(li => li.TicketTypeId is not null))
        {
            return Result<ResolvedOrder>.Failure(Error.Validation("purchase.ticket_type_not_applicable", "Ovaj sektor nema tipove ulaznica."));
        }

        var totalPrice = lineItems.Sum(li =>
            li.Quantity * (li.TicketTypeId is null ? sector.Price : ticketTypesById[li.TicketTypeId.Value].Price));

        return Result<ResolvedOrder>.Success(new ResolvedOrder(reservation, sector, product, ticketTypesById, totalPrice));
    }

    public async Task<Result<PurchaseIntentResponse>> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var resolved = await ResolveOrderAsync(request.HoldId, request.LineItems, user.GetUserId(), ct);
        if (resolved.IsFailure)
            return Result<PurchaseIntentResponse>.Failure(resolved.Error);

        var order = resolved.Value!;
        var orderId = Guid.NewGuid();
        var isSubscription = order.Sector.TicketingMode == TicketingMode.RecurringReservation;

        try
        {
            var intent = await _paymentClient.CreateIntentAsync(
                order.TotalPrice,
                orderId.ToString("N"),
                user.GetUserId(),
                user.GetEmail(),
                $"eKarta — {order.Sector.Name}",
                request.HoldId,
                order.Sector.Id,
                isSubscription,
                isSubscription ? $"{order.Sector.Name} — mjesečna rezervacija" : null,
                ct);

            return Result<PurchaseIntentResponse>.Success(new PurchaseIntentResponse(
                intent.Provider,
                intent.PublishableKey,
                orderId,
                intent.IntentId,
                intent.ClientSecret,
                order.TotalPrice,
                intent.Currency,
                isSubscription));
        }
        catch (PaymentUnavailableException)
        {
            // The hold is deliberately NOT released here. Nothing was charged, the buyer is still on
            // the checkout page, and their reservation should survive a transient payment outage for
            // the rest of its five minutes so they can simply try again.
            return Result<PurchaseIntentResponse>.Failure(Error.Failure("payment.unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
        }
    }

    public async Task<Result<PurchaseResponse>> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var resolved = await ResolveOrderAsync(request.HoldId, request.LineItems, user.GetUserId(), ct);
        if (resolved.IsFailure)
        {
            // The buyer has already confirmed at this point, so an authorization (or, for a
            // subscription, a real charge) is outstanding against an order that will never exist.
            // Undo it before answering, or the money silently sits there.
            await CompensateAsync(request, ct);
            return Result<PurchaseResponse>.Failure(resolved.Error);
        }

        var order = resolved.Value!;
        var sector = order.Sector;
        var ticketTypesById = order.TicketTypesById;
        var reservation = order.Reservation;
        var totalPrice = order.TotalPrice;
        var orderId = request.OrderId;

        var userId = user.GetUserId();
        var userEmail = user.GetEmail();
        var isSubscription = sector.TicketingMode == TicketingMode.RecurringReservation;

        PaymentChargeResponse charge;
        DateOnly? periodStart = null;
        DateOnly? periodEnd = null;

        try
        {
            if (isSubscription)
            {
                var subscriptionCharge = await _paymentClient.ConfirmSubscriptionAsync(
                    request.PaymentIntentId, orderId.ToString("N"), userId, totalPrice, request.SimulatedLast4, ct);

                charge = new PaymentChargeResponse(
                    subscriptionCharge.Id, subscriptionCharge.Amount, subscriptionCharge.Status,
                    subscriptionCharge.OrderRef, subscriptionCharge.Currency, subscriptionCharge.FailureCode);

                periodStart = subscriptionCharge.CurrentPeriodStart;
                periodEnd = subscriptionCharge.CurrentPeriodEnd;
            }
            else
            {
                charge = await _paymentClient.CapturePaymentAsync(
                    request.PaymentIntentId, orderId.ToString("N"), userId, totalPrice, request.SimulatedLast4, ct);
            }
        }
        catch (PaymentUnavailableException)
        {
            await _capacityLock.ReleaseAsync(request.HoldId, ct);
            return Result<PurchaseResponse>.Failure(Error.Failure("payment.unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
        }

        if (charge.Status != PaymentChargeStatus.Succeeded)
        {
            await _capacityLock.ReleaseAsync(request.HoldId, ct);
            await _eventPublisher.PublishAsync(
                EventNames.PaymentFailed,
                new PaymentFailed(userId, userEmail, charge.FailureCode ?? "card_declined", _clock.UtcNow),
                ct);

            // The declined path writes nothing else — no Ticket, no Subscription — so this is the
            // one save that commits the outbox row. Without it the publish above would add a row to
            // the change tracker that nothing ever persists, and the event would silently not
            // happen. See eTicketing.Shared.Messaging.OutboxEventPublisher.
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<PurchaseResponse>.Failure(Error.Validation("payment.declined", "Plaćanje je odbijeno. Provjerite podatke kartice."));
        }

        await _capacityLock.ConfirmAsync(request.HoldId, ct);

        var tickets = new List<Ticket>();
        Subscription? subscription = null;

        foreach (var line in request.LineItems)
        {
            var ticketType = line.TicketTypeId is null ? null : ticketTypesById[line.TicketTypeId.Value];
            var unitPrice = ticketType?.Price ?? sector.Price;

            for (var i = 0; i < line.Quantity; i++)
            {
                Ticket ticket;

                switch (sector.TicketingMode)
                {
                    case TicketingMode.DailyEntry:
                        ticket = Ticket.ForDailyEntry(sector.Id, ticketType?.Id, orderId, sector.ProductId, userId, userEmail, unitPrice, reservation.Date);
                        break;

                    case TicketingMode.RecurringReservation:
                        // Sector.Capacity is always 1 for this mode, so reservation.Quantity is
                        // always 1 too — this branch runs at most once per purchase.
                        subscription ??= new Subscription
                        {
                            Id = Guid.NewGuid(),
                            SectorId = sector.Id,
                            UserId = userId,
                            UserEmail = userEmail,
                            Status = SubscriptionStatus.Active,
                            // The provider's own billing period, not a locally computed one: it is
                            // what the buyer's card statement will say, and every renewal webhook
                            // reports periods on the same schedule. Falling back to the local
                            // business day only covers a provider that reported none.
                            CurrentPeriodStart = periodStart ?? _clock.Today(),
                            CurrentPeriodEnd = periodEnd ?? _clock.Today().AddMonths(1).AddDays(-1),
                            PaymentReference = request.PaymentIntentId,
                            // Kept so cancelling this subscription can hand the space back; the hold
                            // is confirmed (permanent) and cannot be resolved from Redis afterwards.
                            CapacityHoldId = request.HoldId,
                        };
                        subscription.NextRenewalAt = subscription.CurrentPeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

                        ticket = Ticket.ForRecurringReservation(
                            sector.Id, ticketType?.Id, orderId, sector.ProductId, userId, userEmail, unitPrice,
                            subscription.Id, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
                        break;

                    default:
                        ticket = Ticket.ForSingleOccurrence(sector.Id, ticketType?.Id, orderId, sector.ProductId, userId, userEmail, unitPrice);
                        break;
                }

                tickets.Add(ticket);
            }
        }

        if (subscription is not null)
            await _subscriptionRepository.AddAsync(subscription, ct);

        foreach (var ticket in tickets)
            await _ticketRepository.AddAsync(ticket, ct);

        // Read from the clock rather than from tickets[0].CreatedAt, which the audit interceptor
        // only fills in *during* SaveChangesAsync — and the publish below now happens before it.
        // Same instant for the event and the response, so a buyer and their confirmation email
        // never disagree about when the purchase happened.
        var purchasedAt = _clock.UtcNow;

        // Published into the same transaction as the tickets, and therefore before the save rather
        // than after it. Ticket ids are client-generated Guids (see Ticket.ForXxx), so the payload
        // and its signed QR codes are fully known at this point. This is the publish the outbox
        // matters most for: it is the only notice eTicketing.PdfGeneration and
        // eTicketing.Notifications ever get that a real person paid for something.
        // tickets is never empty here: request.LineItems is non-empty (PurchaseRequestValidator)
        // and every line's Quantity > 0, so the nested loop above always adds at least one Ticket.
        await _eventPublisher.PublishAsync(
            EventNames.TicketPurchased,
            new TicketPurchased(
                orderId, sector.ProductId, sector.Id, sector.Name, sector.TicketingMode,
                userId, userEmail, totalPrice, purchasedAt,
                tickets.Select(t => new PurchasedTicket(
                    t.Id,
                    _qrCodec.Sign(t.Id),
                    TicketTypeNameOf(t, ticketTypesById),
                    t.PricePaid, t.ValidDate, t.ValidFrom, t.ValidTo)).ToList(),
                // Carried on the event so eTicketing.PdfGeneration can print a real event name,
                // date and city without a catalogue lookup of its own — and carried as of *now*,
                // which is what a ticket should show even if the product is renamed later.
                order.Product.Name, order.Product.Date, order.Product.City),
            ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // The charge already succeeded (charge.Id) and the hold is already confirmed by this
            // point — a failure here means a paying customer has no Ticket row at all with no other
            // trace of what happened. This must never be silent: log everything needed to manually
            // reconcile the charge, then let it surface as the real bug it is.
            _logger.LogError(
                ex,
                "Uplata {ChargeId} za OrderRef {OrderRef} (hold {HoldId}) je uspjela, ali čuvanje ulaznica u bazu nije uspjelo.",
                charge.Id, orderId, request.HoldId);
            throw;
        }

        var response = new PurchaseResponse(
            orderId, sector.ProductId, sector.Id, totalPrice, purchasedAt,
            tickets.Select(t => _responseFactory.Create(t, sector.Name, TicketTypeNameOf(t, ticketTypesById))).ToList());

        return Result<PurchaseResponse>.Success(response);
    }

    /// <summary>
    /// Undoes an outstanding payment when the order turns out to be unbuyable — almost always
    /// because the five-minute hold lapsed while the buyer was entering their card.
    ///
    /// For a one-time payment this is genuinely free: the intent was authorized with manual capture,
    /// so cancelling it releases a ring-fence and no money ever moved. A subscription cannot work
    /// that way (provider invoices do not support manual capture), so that path really does refund,
    /// and the caller says so in the message the buyer sees.
    ///
    /// Best-effort by design: the buyer is already getting a failure, and an uncaptured authorization
    /// expires on its own. Failing to compensate must not turn a clean "your reservation expired"
    /// into a 503, so it is logged, not thrown.
    /// </summary>
    private async Task CompensateAsync(PurchaseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
            return;

        try
        {
            // A subscription reference, not a one-time intent. Providers prefix these differently,
            // and only the subscription path needs a refund.
            if (request.PaymentIntentId.StartsWith("sub_", StringComparison.Ordinal))
            {
                await _paymentClient.CancelSubscriptionAsync(
                    request.PaymentIntentId, atPeriodEnd: false, refundLastInvoice: true, ct);
            }
            else
            {
                await _paymentClient.CancelIntentAsync(request.PaymentIntentId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Poništavanje plaćanja {PaymentIntentId} nakon neuspjele kupovine (hold {HoldId}) nije uspjelo.",
                request.PaymentIntentId, request.HoldId);
        }
    }

    // The Tickets minted above are brand-new and untracked-by-navigation, so TicketType is null on
    // them even though TicketTypeId is set — resolve the name from the dictionary the sector was
    // loaded with rather than through the (unloaded) navigation.
    private static string? TicketTypeNameOf(Ticket ticket, IReadOnlyDictionary<Guid, TicketType> ticketTypesById) =>
        ticket.TicketTypeId is null ? null : ticketTypesById[ticket.TicketTypeId.Value].Name;
}
