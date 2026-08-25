using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Purchases;

/// <summary>Mirrors eTicketing.Identity.Business.Auth.AuthService's own inline IEventPublisher —
/// duplicated per-service by design (see RabbitMqEventPublisher's doc comment), not shared/promoted
/// to Contracts.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates POST /purchases — the synchronous purchase critical path from
/// .claude/rules/01-domain.md, minus real payment (eTicketing.Payment is an internally-mocked
/// service, see docs/payment-setup-guide.md). Step by step:
///  1. Resolve the hold's actual reservation from Redis via ISectorCapacityLock.PeekAsync — never
///     trust a client-supplied SectorId/quantity (see PeekAsync's own doc comment for why). Also
///     the guard against a replayed HoldId: PeekAsync returns null once ConfirmAsync has already
///     run for it (see RedisSectorCapacityLock.ConfirmAsync).
///  2. Validate the request's line-item quantities sum to exactly what was held.
///  3. Load the held Sector (with TicketTypes) — must still exist and be Published.
///  4. Validate each line's TicketTypeId against the Sector's TicketTypes (all-or-nothing: either
///     every line needs one, or none do — the request-shape half of that rule lives in
///     PurchaseRequestValidator, the DB-dependent half lives here).
///  5. Compute the total price.
///  6. Charge via IPaymentClient — a circuit-open/timeout/transport failure releases the hold and
///     returns a 503 (Error.Failure); this is a service-outage, not the buyer's fault. PaymentService
///     on the other side is itself idempotent on OrderRef, so a retried charge after a lost response
///     never double-charges.
///  7. A card the mock declines releases the hold and returns 400 (Error.Validation) — the buyer's
///     problem, not an outage; deliberately different HTTP semantics from step 6.
///  8. Success confirms the hold (permanent capacity decrement).
///  9. Mints one Ticket per admission unit (not one Ticket with a Quantity field — each unit needs
///     its own QR/receipt identity, and TicketPurchased has no Quantity field to carry one anyway)
///     via the mode-specific Ticket.ForXxx factory. RecurringReservation additionally creates one
///     Subscription shared by that single Ticket (Capacity is always 1 for that mode).
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

    public async Task<Result<PurchaseResponse>> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var reservation = await _capacityLock.PeekAsync(request.HoldId, ct);
        if (reservation is null)
            return Result<PurchaseResponse>.Failure(Error.Validation("purchase.hold_expired", "Rezervacija je istekla ili ne postoji. Pokušajte ponovo."));

        var requestedQuantity = request.LineItems.Sum(li => li.Quantity);
        if (requestedQuantity != reservation.Quantity)
            return Result<PurchaseResponse>.Failure(Error.Validation("purchase.quantity_mismatch", "Količina se ne poklapa sa rezervacijom."));

        var sector = await _sectorRepository.GetByIdWithTicketTypesAsync(reservation.SectorId, ct);
        if (sector is null || sector.Status != PublishStatus.Published)
            return Result<PurchaseResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ticketTypesById = sector.TicketTypes.ToDictionary(t => t.Id);
        if (ticketTypesById.Count > 0)
        {
            if (request.LineItems.Any(li => li.TicketTypeId is null || !ticketTypesById.ContainsKey(li.TicketTypeId.Value)))
                return Result<PurchaseResponse>.Failure(Error.Validation("purchase.invalid_ticket_type", "Odabrani tip ulaznice ne pripada ovom sektoru."));
        }
        else if (request.LineItems.Any(li => li.TicketTypeId is not null))
        {
            return Result<PurchaseResponse>.Failure(Error.Validation("purchase.ticket_type_not_applicable", "Ovaj sektor nema tipove ulaznica."));
        }

        var totalPrice = request.LineItems.Sum(li =>
            li.Quantity * (li.TicketTypeId is null ? sector.Price : ticketTypesById[li.TicketTypeId.Value].Price));
        var orderId = Guid.NewGuid();

        PaymentChargeResponse charge;
        try
        {
            charge = await _paymentClient.ChargeAsync(totalPrice, orderId.ToString("N"), Last4(request.CardNumber), ct);
        }
        catch (PaymentUnavailableException)
        {
            await _capacityLock.ReleaseAsync(request.HoldId, ct);
            return Result<PurchaseResponse>.Failure(Error.Failure("payment.unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
        }

        var userId = user.GetUserId();
        var userEmail = user.GetEmail();

        if (charge.Status == PaymentChargeStatus.Failed)
        {
            await _capacityLock.ReleaseAsync(request.HoldId, ct);
            await _eventPublisher.PublishAsync(EventNames.PaymentFailed, new PaymentFailed(userId, userEmail, "card_declined", _clock.UtcNow), ct);
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
                            Status = SubscriptionStatus.Active,
                            // Local business day, not UTC — a subscription bought just after local
                            // midnight must bill from today, not from yesterday. See PlatformClock.
                            CurrentPeriodStart = _clock.Today(),
                            CurrentPeriodEnd = _clock.Today().AddMonths(1).AddDays(-1),
                            PaymentReference = charge.Id.ToString(),
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

        // tickets is never empty here: request.LineItems is non-empty (PurchaseRequestValidator)
        // and every line's Quantity > 0, so the nested loop above always adds at least one Ticket.
        await _eventPublisher.PublishAsync(
            EventNames.TicketPurchased,
            new TicketPurchased(
                orderId, sector.ProductId, sector.Id, sector.Name, sector.TicketingMode,
                userId, userEmail, totalPrice, tickets[0].CreatedAt,
                tickets.Select(t => new PurchasedTicket(
                    t.Id,
                    _qrCodec.Sign(t.Id),
                    TicketTypeNameOf(t, ticketTypesById),
                    t.PricePaid, t.ValidDate, t.ValidFrom, t.ValidTo)).ToList()),
            ct);

        var response = new PurchaseResponse(
            orderId, sector.ProductId, sector.Id, totalPrice, tickets[0].CreatedAt,
            tickets.Select(t => _responseFactory.Create(t, sector.Name, TicketTypeNameOf(t, ticketTypesById))).ToList());

        return Result<PurchaseResponse>.Success(response);
    }

    // The Tickets minted above are brand-new and untracked-by-navigation, so TicketType is null on
    // them even though TicketTypeId is set — resolve the name from the dictionary the sector was
    // loaded with rather than through the (unloaded) navigation.
    private static string? TicketTypeNameOf(Ticket ticket, IReadOnlyDictionary<Guid, TicketType> ticketTypesById) =>
        ticket.TicketTypeId is null ? null : ticketTypesById[ticket.TicketTypeId.Value].Name;

    // The `cardNumber.Length >= 4` branch below is only a defensive fallback — in practice
    // CardNumber always has at least 12 digits by the time it reaches here, enforced by
    // PurchaseRequestValidator's `^\d{12,19}$` rule, which runs before this service method does.
    private static string Last4(string cardNumber) => cardNumber.Length >= 4 ? cardNumber[^4..] : cardNumber;
}
