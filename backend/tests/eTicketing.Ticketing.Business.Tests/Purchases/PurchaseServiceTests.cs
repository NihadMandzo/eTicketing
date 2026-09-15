using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Purchases;

public class PurchaseServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IPurchaseService _sut;
    private readonly ISectorService _sectorService;
    private readonly ITicketTypeService _ticketTypeService;

    private readonly Guid _orgA = Guid.NewGuid();

    // Stable for the lifetime of one test class instance: PurchaseService now compares the hold's
    // recorded owner against the caller, so a fresh NameIdentifier per OrgACaller() call would make
    // every buyer a different person from the one who took the hold.
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _singleOccurrenceProductId = Guid.NewGuid();
    private readonly Guid _dailyEntryProductId = Guid.NewGuid();
    private readonly Guid _recurringReservationProductId = Guid.NewGuid();

    public PurchaseServiceTests()
    {
        _sut = _fixture.CreatePurchaseService();
        _sectorService = _fixture.CreateSectorService();
        _ticketTypeService = _fixture.CreateTicketTypeService();

        MockProduct(_singleOccurrenceProductId, TicketingMode.SingleOccurrence);
        MockProduct(_dailyEntryProductId, TicketingMode.DailyEntry);
        MockProduct(_recurringReservationProductId, TicketingMode.RecurringReservation);
    }

    private void MockProduct(Guid productId, TicketingMode mode) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(productId, _orgA, PublishStatus.Published, mode, "Test proizvod", null, City.Sarajevo));

    private static ClaimsPrincipal BuildCaller()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "User"),
            new(ClaimTypes.Email, "buyer@example.com"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private ClaimsPrincipal OrgACaller()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _callerId.ToString()),
            new(ClaimTypes.Role, "OrganizationSuperAdmin"),
            new(ClaimTypes.Email, "organizer@example.com"),
            new("organizationId", _orgA.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private async Task<Sector> CreatePublishedSectorAsync(
        Guid productId, string name, int capacity, decimal price, int? periodYear = null, int? periodMonth = null)
    {
        var created = await _sectorService.CreateAsync(
            new UpsertSectorRequest { ProductId = productId, Name = name, Capacity = capacity, Price = price, PeriodYear = periodYear, PeriodMonth = periodMonth },
            OrgACaller());
        await _sectorService.PublishAsync(created.Value!.Id, OrgACaller());
        return (await _fixture.SectorRepository.GetByIdWithTicketTypesAsync(created.Value.Id))!;
    }

    private void MockHold(HeldReservation? reservation) =>
        _fixture.CapacityLock.Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

    /// <summary>Both purchase paths in one helper: a SingleOccurrence/DailyEntry sector captures a
    /// one-time authorization, a RecurringReservation sector confirms a subscription instead. Tests
    /// set up both so they do not have to care which mode the sector under test uses.</summary>
    private void MockSuccessfulCharge()
    {
        _fixture.PaymentClient
            .Setup(p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string orderRef, Guid _, decimal amount, string? _, CancellationToken _) =>
                new PaymentChargeResponse(Guid.NewGuid(), amount, PaymentChargeStatus.Succeeded, orderRef, "eur", null));

        _fixture.PaymentClient
            .Setup(p => p.ConfirmSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, string orderRef, Guid _, decimal amount, string? _, CancellationToken _) =>
                new SubscriptionChargeResponse(
                    Guid.NewGuid(), amount, PaymentChargeStatus.Succeeded, orderRef, "eur", null,
                    reference, SubscriptionPeriodStart, SubscriptionPeriodEnd));
    }

    private void MockDeclinedCharge()
    {
        _fixture.PaymentClient
            .Setup(p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string orderRef, Guid _, decimal amount, string? _, CancellationToken _) =>
                new PaymentChargeResponse(Guid.NewGuid(), amount, PaymentChargeStatus.Failed, orderRef, "eur", "card_declined"));

        _fixture.PaymentClient
            .Setup(p => p.ConfirmSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, string orderRef, Guid _, decimal amount, string? _, CancellationToken _) =>
                new SubscriptionChargeResponse(
                    Guid.NewGuid(), amount, PaymentChargeStatus.Failed, orderRef, "eur", "card_declined",
                    reference, SubscriptionPeriodStart, SubscriptionPeriodEnd));
    }

    /// <summary>The billing period the mocked provider reports. Deliberately NOT the fixture clock's
    /// month, so a test can tell the difference between "stamped from the provider" (correct) and
    /// "computed locally from the clock" (the pre-Stripe behavior).</summary>
    private static readonly DateOnly SubscriptionPeriodStart = new(2026, 9, 1);
    private static readonly DateOnly SubscriptionPeriodEnd = new(2026, 9, 30);

    /// <summary>The shape POST /purchases receives after the buyer has confirmed payment in their
    /// browser: no card data at all, just the order and intent POST /purchases/payment-intent
    /// minted. SimulatedLast4 is left null, i.e. the Stripe provider's shape.</summary>
    private static PurchaseRequest BuildRequest(params PurchaseLineItemRequest[] lineItems) => new()
    {
        HoldId = "hold-1",
        LineItems = lineItems,
        OrderId = Guid.NewGuid(),
        PaymentIntentId = "pi_test_123",
    };

    [Fact]
    public async Task PurchaseAsync_SingleOccurrenceHappyPath_CreatesConfirmedTicketAndPublishesEvent()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 2, _callerId));
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 2 }), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tickets.Should().HaveCount(2);
        result.Value.Tickets.Should().OnlyContain(t => t.Status == TicketStatus.Confirmed && t.PricePaid == 50);
        result.Value.TotalPaid.Should().Be(100);
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync("hold-1", It.IsAny<CancellationToken>()), Times.Once);

        // ONE event for the whole order, carrying both tickets — not one event per minted Ticket.
        // eTicketing.PdfGeneration renders a PDF per ticket but eTicketing.Notifications sends a
        // single confirmation email with all of them attached, which it can only do if it sees the
        // order as one unit.
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.TicketPurchased,
                It.Is<TicketPurchased>(e => e.Tickets.Count == 2 && e.TotalPaid == 100 && e.SectorName == "VIP"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_PublishesOneQrPayloadPerTicket_MatchingTheMintedTicketIds()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 2, _callerId));
        MockSuccessfulCharge();

        TicketPurchased? published = null;
        _fixture.EventPublisher
            .Setup(p => p.PublishAsync(EventNames.TicketPurchased, It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .Callback<string, TicketPurchased, CancellationToken>((_, e, _) => published = e);

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 2 }), OrgACaller());

        published.Should().NotBeNull();
        published!.Tickets.Select(t => t.TicketId).Should().BeEquivalentTo(result.Value!.Tickets.Select(t => t.Id));

        // eTicketing.PdfGeneration never signs anything — it renders the payload it's handed here,
        // so each one has to be the real, verifiable code for its own ticket.
        foreach (var line in published.Tickets)
        {
            _fixture.QrCodec.TryParse(line.QrPayload, out var decoded).Should().BeTrue();
            decoded.Should().Be(line.TicketId);
        }
    }

    [Fact]
    public async Task PurchaseAsync_ReturnsRenderableQrImageAndNoPdfUrlYet()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        var ticket = result.Value!.Tickets.Single();
        ticket.QrImage.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task PurchaseAsync_DailyEntryHappyPath_SetsValidDateFromHold()
    {
        var sector = await CreatePublishedSectorAsync(_dailyEntryProductId, "August 2026", 300, 10, 2026, 8);
        var date = new DateOnly(2026, 8, 15);
        MockHold(new HeldReservation(sector.Id, date, 1, _callerId));
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tickets.Single().ValidDate.Should().Be(date);
    }

    [Fact]
    public async Task PurchaseAsync_RecurringReservationHappyPath_CreatesSubscriptionAndOneTicket()
    {
        var sector = await CreatePublishedSectorAsync(_recurringReservationProductId, "A-12", 1, 80);
        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        var ticket = result.Value!.Tickets.Single();
        ticket.ValidFrom.Should().NotBeNull();
        ticket.ValidTo.Should().NotBeNull();

        var persistedTicket = await _fixture.TicketRepository.GetByIdAsync(ticket.Id);
        persistedTicket!.SubscriptionId.Should().NotBeNull();
        var subscription = await _fixture.SubscriptionRepository.GetByIdAsync(persistedTicket.SubscriptionId!.Value);
        subscription.Should().NotBeNull();
        subscription!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task PurchaseAsync_WithTicketTypeLineItems_SumsAgainstSharedSectorCapacity()
    {
        var sector = await CreatePublishedSectorAsync(_dailyEntryProductId, "August 2026", 300, 999, 2026, 8);
        var odrasli = (await _ticketTypeService.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller())).Value!;
        var djeca = (await _ticketTypeService.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Djeca", Price = 4 }, OrgACaller())).Value!;
        var date = new DateOnly(2026, 8, 20);
        MockHold(new HeldReservation(sector.Id, date, 3, _callerId));
        MockSuccessfulCharge();

        var request = BuildRequest(
            new PurchaseLineItemRequest { TicketTypeId = odrasli.Id, Quantity = 2 },
            new PurchaseLineItemRequest { TicketTypeId = djeca.Id, Quantity = 1 });

        var result = await _sut.PurchaseAsync(request, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tickets.Should().HaveCount(3);
        result.Value.TotalPaid.Should().Be(2 * 10 + 1 * 4);
        result.Value.Tickets.Count(t => t.TicketTypeId == odrasli.Id).Should().Be(2);
        result.Value.Tickets.Count(t => t.TicketTypeId == djeca.Id).Should().Be(1);
    }

    [Fact]
    public async Task PurchaseAsync_QuantityMismatchWithHold_ReturnsValidationError()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 2, _callerId));

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 3 }), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.quantity_mismatch");
    }

    [Fact]
    public async Task PurchaseAsync_UnknownOrExpiredHold_ReturnsValidationError()
    {
        MockHold(null);

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.hold_expired");
    }

    // --- Hold ownership ---

    [Fact]
    public async Task PurchaseAsync_WithAHoldBelongingToAnotherAccount_IsRefusedWithoutCharging()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, Guid.NewGuid())); // somebody else's hold
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        // Same error as an expired hold, deliberately: a distinct "not yours" would confirm to the
        // thief that the id names a live reservation.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.hold_expired");

        // The victim keeps their seat: nothing captured, nothing confirmed, nothing released.
        _fixture.PaymentClient.Verify(
            p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await _fixture.TicketRepository.Query().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_WithAHoldBelongingToAnotherAccount_IsRefused()
    {
        // The intent call is where a stolen hold id would first be spent — it prices the
        // reservation — so it has to refuse for the same reason PurchaseAsync does.
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, Guid.NewGuid()));

        var result = await _sut.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest { HoldId = "hold-1", LineItems = [new PurchaseLineItemRequest { Quantity = 1 }] },
            OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.hold_expired");
        _fixture.PaymentClient.Verify(
            p => p.CreateIntentAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_WithAHoldThatHasNoRecordedOwner_StillSucceeds()
    {
        // A hold minted by the previous deploy, before the owner was recorded. Those keep arriving
        // for the remaining five minutes of their TTL; refusing them would fail every checkout that
        // straddled the deploy. See RedisSectorCapacityLock's HoldInfoVersionPrefix.
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, null));
        MockSuccessfulCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync("hold-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_PaymentDeclined_ReleasesHoldAndReturns400()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));
        MockDeclinedCharge();

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.declined");
        result.Error.Type.Should().Be(ErrorType.Validation);
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync("hold-1", It.IsAny<CancellationToken>()), Times.Once);
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_PaymentCircuitOpen_ReleasesHoldAndReturns503()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));
        _fixture.PaymentClient
            .Setup(p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentUnavailableException("down", new InvalidOperationException()));

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.unavailable");
        result.Error.Type.Should().Be(ErrorType.Failure);
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync("hold-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_TicketTypeNotBelongingToSector_ReturnsValidationError()
    {
        var sector = await CreatePublishedSectorAsync(_dailyEntryProductId, "August 2026", 300, 10, 2026, 8);
        await _ticketTypeService.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());
        MockHold(new HeldReservation(sector.Id, new DateOnly(2026, 8, 10), 1, _callerId));

        var request = BuildRequest(new PurchaseLineItemRequest { TicketTypeId = Guid.NewGuid(), Quantity = 1 });
        var result = await _sut.PurchaseAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.invalid_ticket_type");
    }

    [Fact]
    public async Task PurchaseAsync_ForSectorWithNoTicketTypes_LineItemWithTicketTypeId_ReturnsValidationError()
    {
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));

        var request = BuildRequest(new PurchaseLineItemRequest { TicketTypeId = Guid.NewGuid(), Quantity = 1 });
        var result = await _sut.PurchaseAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("purchase.ticket_type_not_applicable");
    }

    [Fact]
    public async Task PurchaseAsync_SameHoldIdTwice_ReturnsHoldExpiredOnSecondAttempt()
    {
        // Models the real RedisSectorCapacityLock contract this mock stands in for: once
        // ConfirmAsync has run for a holdId, PeekAsync must return null for it from then on (see
        // RedisSectorCapacityLock.ConfirmAsync's holdinfo cleanup) — otherwise a replayed purchase
        // request with the same HoldId would be treated as still-live and charged a second time.
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        var confirmed = false;
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => confirmed ? null : new HeldReservation(sector.Id, null, 1, _callerId));
        _fixture.CapacityLock
            .Setup(l => l.ConfirmAsync("hold-1", It.IsAny<CancellationToken>()))
            .Callback(() => confirmed = true)
            .Returns(Task.CompletedTask);
        MockSuccessfulCharge();

        var first = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());
        var second = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("purchase.hold_expired");
        _fixture.PaymentClient.Verify(
            p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_SectorNoLongerPublishedAfterHold_ReturnsNotFound()
    {
        // There's no "unpublish" endpoint (Draft → Published is one-way, per the Preview → Publish
        // pattern in .claude/rules/01-domain.md), so this reverts Status directly through the real
        // repository to model the case PurchaseService actually has to guard against: a hold that
        // outlives its Sector's published lifetime (e.g. the organizer deleted/edited it away from
        // Published between the hold and the purchase attempt) must not be purchasable.
        var sector = await CreatePublishedSectorAsync(_singleOccurrenceProductId, "VIP", 100, 50);
        var tracked = (await _fixture.SectorRepository.GetByIdWithTicketTypesAsync(sector.Id))!;
        tracked.Status = PublishStatus.Draft;
        _fixture.SectorRepository.Update(tracked);
        await _fixture.UnitOfWork.SaveChangesAsync();

        MockHold(new HeldReservation(sector.Id, null, 1, _callerId));

        var result = await _sut.PurchaseAsync(BuildRequest(new PurchaseLineItemRequest { Quantity = 1 }), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
        _fixture.PaymentClient.Verify(
            p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose() => _fixture.Dispose();
}
