using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

/// <summary>
/// How many times one ticket opens the gate, which is not the same question for every
/// TicketingMode.
///
/// SingleOccurrence and DailyEntry are one admission each: scan, burn, done — the behaviour
/// <see cref="TicketValidationServiceTests"/> already covers and which these tests re-assert from
/// the "how many passages" angle so a regression toward toggling them shows up here.
///
/// RecurringReservation is a month of parking, and burning it on the first drive-in would lock its
/// holder out for the remaining 29 days. Those tickets toggle instead: a scan while outside is an
/// entry, a scan while inside is an exit, and the ticket stays Confirmed throughout. That toggle is
/// also the enforcement mechanism for the rule these tests exist for — the holder must exit before
/// they can enter again, and because direction is read off stored state rather than sent by the
/// scanner, there is no request a caller could make to enter twice in a row.
/// </summary>
public class TicketGateAdmissionTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketValidationService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _scannerUserId = Guid.NewGuid();

    /// <summary>Matches TicketingTestContext.Clock's pinned start.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public TicketGateAdmissionTests()
    {
        _sut = _fixture.CreateTicketValidationService();
        MockProduct(TicketingMode.SingleOccurrence, Today.ToDateTime(new TimeOnly(20, 0)));
    }

    // ------------------------------------------- RecurringReservation: the entry/exit toggle

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketsFirstScan_AdmitsItWithoutBurningIt()
    {
        var ticket = await SeedSubscriptionTicketAsync();

        var result = await _sut.ValidateAsync(Request(_fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.Code.Should().Be("ticket.valid");
        result.Value.Message.Should().Be("Ulaznica je validna. Ulaz odobren.");
        result.Value.Direction.Should().Be(GatePassageDirection.Entry);
        result.Value.IsInside.Should().BeTrue();

        // The whole point: a monthly parking pass must survive its own first use.
        var persisted = await ReloadAsync(ticket.Id);
        persisted.Status.Should().Be(TicketStatus.Confirmed);
        persisted.IsInside.Should().BeTrue();
        persisted.EntryCount.Should().Be(1);
        persisted.LastEntryAt.Should().Be(_fixture.Clock.GetUtcNow().UtcDateTime);
        persisted.LastExitAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketScannedWhileInside_RecordsAnExitRatherThanASecondEntry()
    {
        // "The same ticket cannot be used for entry twice without an intervening exit" — the
        // second scan is that exit, so there is no second entry to make.
        var ticket = await SeedSubscriptionTicketAsync();
        var code = _fixture.QrCodec.Sign(ticket.Id);
        await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        var second = await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        second.Value!.IsValid.Should().BeTrue();
        second.Value.Code.Should().Be("ticket.exit_recorded");
        second.Value.Message.Should().Be("Izlaz zabilježen. Ulaznica je ponovo spremna za ulaz.");
        second.Value.Direction.Should().Be(GatePassageDirection.Exit);
        second.Value.IsInside.Should().BeFalse();

        var persisted = await ReloadAsync(ticket.Id);
        persisted.IsInside.Should().BeFalse();
        // Not incremented by the exit — EntryCount counts admissions, not scans.
        persisted.EntryCount.Should().Be(1);
        persisted.LastExitAt.Should().Be(_fixture.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketAfterAnExit_AdmitsTheHolderAgain()
    {
        var ticket = await SeedSubscriptionTicketAsync();
        var code = _fixture.QrCodec.Sign(ticket.Id);
        await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA)); // in
        await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA)); // out

        var third = await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        third.Value!.IsValid.Should().BeTrue();
        third.Value.Direction.Should().Be(GatePassageDirection.Entry);

        var persisted = await ReloadAsync(ticket.Id);
        persisted.IsInside.Should().BeTrue();
        persisted.EntryCount.Should().Be(2);
        persisted.Status.Should().Be(TicketStatus.Confirmed);
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketOverManyDays_KeepsWorkingForItsWholePeriod()
    {
        // A month of parking is roughly 40 passages. None of them may exhaust the ticket.
        var ticket = await SeedSubscriptionTicketAsync();
        var code = _fixture.QrCodec.Sign(ticket.Id);

        for (var i = 0; i < 40; i++)
            await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        var persisted = await ReloadAsync(ticket.Id);
        persisted.Status.Should().Be(TicketStatus.Confirmed);
        persisted.EntryCount.Should().Be(20);
        // Even number of passages, so the holder ended up outside.
        persisted.IsInside.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketPastItsPeriod_RefusesEntryEvenWhenNeverUsed()
    {
        var ticket = await SeedSubscriptionTicketAsync(validFrom: Today.AddDays(-40), validTo: Today.AddDays(-10));

        var result = await _sut.ValidateAsync(Request(_fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
        result.Value.Direction.Should().Be(GatePassageDirection.None);

        var persisted = await ReloadAsync(ticket.Id);
        persisted.IsInside.Should().BeFalse();
        persisted.EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketWhosePeriodLapsedWhileInside_RefusesTheExitScanToo()
    {
        // Deliberate: the date window is checked before direction, so a holder still marked inside
        // when their period ends does not get a free "valid" scan the next morning. The gate says
        // no and a human sorts it out — which is better than a green card that reads as admission.
        var ticket = await SeedSubscriptionTicketAsync(validFrom: Today.AddDays(-30), validTo: Today);
        var code = _fixture.QrCodec.Sign(ticket.Id);
        await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));
        _fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero));

        var result = await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
        (await ReloadAsync(ticket.Id)).IsInside.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ForACancelledSubscriptionTicket_RefusesEntry()
    {
        var ticket = await SeedSubscriptionTicketAsync(status: TicketStatus.Cancelled);

        var result = await _sut.ValidateAsync(Request(_fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.cancelled");
        (await ReloadAsync(ticket.Id)).EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketOfAnotherOrganization_RefusesBeforeTouchingAdmissionState()
    {
        var ticket = await SeedSubscriptionTicketAsync(organizationId: Guid.NewGuid());

        var result = await _sut.ValidateAsync(Request(_fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_organization");

        var persisted = await ReloadAsync(ticket.Id);
        persisted.IsInside.Should().BeFalse();
        persisted.EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_ForASubscriptionTicketScannedAtAGateDevice_AttributesBothDirectionsToThatDevice()
    {
        var ticket = await SeedSubscriptionTicketAsync();
        var device = SeedGateDevice();
        var code = _fixture.QrCodec.Sign(ticket.Id);

        var entry = await _sut.ValidateForDeviceAsync(device, code);
        var afterEntry = await ReloadAsync(ticket.Id);
        var exit = await _sut.ValidateForDeviceAsync(device, code);

        entry.Value!.Direction.Should().Be(GatePassageDirection.Entry);
        exit.Value!.Direction.Should().Be(GatePassageDirection.Exit);

        afterEntry.ValidatedByDeviceId.Should().Be(device.Id);
        // An exit is as much a scan as an entry, so "last seen at this gate" has to move with it.
        var afterExit = await ReloadAsync(ticket.Id);
        afterExit.ValidatedByDeviceId.Should().Be(device.Id);
        afterExit.ValidatedByUserId.Should().Be(device.CreatedByUserId);
    }

    // --------------------------------- SingleOccurrence / DailyEntry: still exactly one passage

    [Fact]
    public async Task ValidateAsync_ForASingleOccurrenceTicket_BurnsItOnTheOnlyScan()
    {
        var ticket = await SeedOneShotTicketAsync(TicketingMode.SingleOccurrence);

        var result = await _sut.ValidateAsync(Request(_fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.Direction.Should().Be(GatePassageDirection.Entry);
        // No entry/exit state at all for a one-shot ticket — the burn is the whole record.
        result.Value.IsInside.Should().BeFalse();

        var persisted = await ReloadAsync(ticket.Id);
        persisted.Status.Should().Be(TicketStatus.Used);
        persisted.IsInside.Should().BeFalse();
        persisted.EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_ForADailyEntryTicketScannedTwice_RejectsTheSecondScanRatherThanTreatingItAsAnExit()
    {
        // The bright line between the two behaviours: a museum day pass is one admission, so its
        // second scan must be the "already used" refusal, never the exit a parking pass would get.
        MockProduct(TicketingMode.DailyEntry, date: null);
        var ticket = await SeedOneShotTicketAsync(TicketingMode.DailyEntry, validDate: Today);
        var code = _fixture.QrCodec.Sign(ticket.Id);
        await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        var second = await _sut.ValidateAsync(Request(code), OrganizerOf(_orgA));

        second.Value!.IsValid.Should().BeFalse();
        second.Value.Code.Should().Be("ticket.already_used");
        second.Value.Direction.Should().Be(GatePassageDirection.None);
    }

    // ------------------------------------------------------------------------------ test setup

    private ValidateTicketRequest Request(string code) => new() { ProductId = _productId, Code = code };

    private async Task<Ticket> SeedSubscriptionTicketAsync(
        Guid? organizationId = null,
        TicketStatus status = TicketStatus.Confirmed,
        DateOnly? validFrom = null,
        DateOnly? validTo = null)
    {
        var sector = SeedSector(TicketingMode.RecurringReservation, organizationId);
        var ticket = Ticket.ForRecurringReservation(
            sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "buyer@example.com", 120,
            SeedSubscription(sector.Id), validFrom ?? Today.AddDays(-10), validTo ?? Today.AddDays(10));
        ticket.Status = status;
        return await PersistAsync(ticket);
    }

    private async Task<Ticket> SeedOneShotTicketAsync(TicketingMode mode, DateOnly? validDate = null)
    {
        var sector = SeedSector(mode, null);
        var ticket = mode == TicketingMode.DailyEntry
            ? Ticket.ForDailyEntry(sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "buyer@example.com", 20, validDate)
            : Ticket.ForSingleOccurrence(sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "buyer@example.com", 50);
        return await PersistAsync(ticket);
    }

    private Sector SeedSector(TicketingMode mode, Guid? organizationId)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = organizationId ?? _orgA,
            Name = "A-12",
            Capacity = 1,
            Price = 120,
            Status = PublishStatus.Published,
            TicketingMode = mode,
        };
        _fixture.DbContext.Sectors.Add(sector);
        return sector;
    }

    private Guid SeedSubscription(Guid sectorId)
    {
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            UserId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = Today.AddDays(-10),
            CurrentPeriodEnd = Today.AddDays(10),
        };
        _fixture.DbContext.Subscriptions.Add(subscription);
        return subscription.Id;
    }

    private GateDevice SeedGateDevice()
    {
        var device = new GateDevice
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgA,
            ProductId = _productId,
            Name = "Rampa A",
            KeyHash = "hash",
            KeyPrefix = "etk_",
            AllSectors = true,
            CreatedByUserId = Guid.NewGuid(),
        };
        _fixture.DbContext.GateDevices.Add(device);
        _fixture.DbContext.SaveChanges();
        _fixture.DbContext.ChangeTracker.Clear();
        return device;
    }

    private async Task<Ticket> PersistAsync(Ticket ticket)
    {
        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return ticket;
    }

    private async Task<Ticket> ReloadAsync(Guid ticketId)
    {
        _fixture.DbContext.ChangeTracker.Clear();
        return await _fixture.DbContext.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticketId);
    }

    private void MockProduct(TicketingMode mode, DateTime? date)
    {
        var response = new CatalogProductResponse(
            _productId, _orgA, PublishStatus.Published, mode, "Test proizvod", date, City.Sarajevo);

        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(_productId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([response]);
    }

    private ClaimsPrincipal OrganizerOf(Guid organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
            new(ClaimTypes.Email, "gate@example.com"),
            new("organizationId", organizationId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
