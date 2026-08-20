using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;

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
///     trust a client-supplied SectorId/quantity (see PeekAsync's own doc comment for why).
///  2. Validate the request's line-item quantities sum to exactly what was held.
///  3. Load the held Sector (with TicketTypes) — must still exist and be Published.
///  4. Validate each line's TicketTypeId against the Sector's TicketTypes (all-or-nothing: either
///     every line needs one, or none do).
///  5. Compute the total price.
///  6. Charge via IPaymentClient — a circuit-open/timeout/transport failure releases the hold and
///     returns a 503 (Error.Failure); this is a service-outage, not the buyer's fault.
///  7. A card the mock declines releases the hold and returns 400 (Error.Validation) — the buyer's
///     problem, not an outage; deliberately different HTTP semantics from step 6.
///  8. Success confirms the hold (permanent capacity decrement).
///  9. Mints one Ticket per admission unit (not one Ticket with a Quantity field — see the
///     reasoning in the approved plan: TicketPurchased already has no Quantity field, each unit
///     needs its own QR/receipt identity). RecurringReservation additionally creates one
///     Subscription shared by that single Ticket (Capacity is always 1 for that mode).
/// 10. Persists everything in one SaveChangesAsync.
/// 11. Publishes TicketPurchased once per minted Ticket.
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

    public PurchaseService(
        ISectorRepository sectorRepository,
        ITicketRepository ticketRepository,
        ISubscriptionRepository subscriptionRepository,
        ISectorCapacityLock capacityLock,
        IPaymentClient paymentClient,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork)
    {
        _sectorRepository = sectorRepository;
        _ticketRepository = ticketRepository;
        _subscriptionRepository = subscriptionRepository;
        _capacityLock = capacityLock;
        _paymentClient = paymentClient;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
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
            await _eventPublisher.PublishAsync(EventNames.PaymentFailed, new PaymentFailed(userId, userEmail, "card_declined", DateTime.UtcNow), ct);
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
                var ticket = new Ticket
                {
                    Id = Guid.NewGuid(),
                    SectorId = sector.Id,
                    TicketTypeId = ticketType?.Id,
                    OrderId = orderId,
                    ProductId = sector.ProductId,
                    UserId = userId,
                    UserEmail = userEmail,
                    Status = TicketStatus.Confirmed,
                    PricePaid = unitPrice,
                };

                switch (sector.TicketingMode)
                {
                    case TicketingMode.DailyEntry:
                        ticket.ValidDate = reservation.Date;
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
                            CurrentPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow),
                            CurrentPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1).AddDays(-1),
                            PaymentReference = charge.Id.ToString(),
                        };
                        subscription.NextRenewalAt = subscription.CurrentPeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

                        ticket.SubscriptionId = subscription.Id;
                        ticket.ValidFrom = subscription.CurrentPeriodStart;
                        ticket.ValidTo = subscription.CurrentPeriodEnd;
                        break;
                }

                tickets.Add(ticket);
            }
        }

        if (subscription is not null)
            await _subscriptionRepository.AddAsync(subscription, ct);

        foreach (var ticket in tickets)
            await _ticketRepository.AddAsync(ticket, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var ticket in tickets)
        {
            await _eventPublisher.PublishAsync(
                EventNames.TicketPurchased,
                new TicketPurchased(ticket.Id, ticket.ProductId, ticket.SectorId, ticket.UserId, ticket.UserEmail, ticket.CreatedAt, ticket.ValidDate, ticket.ValidFrom, ticket.ValidTo),
                ct);
        }

        var response = new PurchaseResponse(
            orderId, sector.ProductId, sector.Id, totalPrice, tickets[0].CreatedAt,
            tickets.Select(t => ToTicketResponse(t, sector.Name, ticketTypesById)).ToList());

        return Result<PurchaseResponse>.Success(response);
    }

    private static TicketResponse ToTicketResponse(Ticket ticket, string sectorName, IReadOnlyDictionary<Guid, TicketType> ticketTypesById) =>
        new(
            ticket.Id, ticket.OrderId, ticket.SectorId, sectorName, ticket.ProductId,
            ticket.TicketTypeId, ticket.TicketTypeId is null ? null : ticketTypesById[ticket.TicketTypeId.Value].Name,
            ticket.Status, ticket.PricePaid, ticket.ValidDate, ticket.ValidFrom, ticket.ValidTo, ticket.CreatedAt);

    private static string Last4(string cardNumber) => cardNumber.Length >= 4 ? cardNumber[^4..] : cardNumber;
}
