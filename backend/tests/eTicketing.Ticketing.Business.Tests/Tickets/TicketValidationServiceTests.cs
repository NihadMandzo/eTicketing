using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

/// <summary>
/// The gate. Note the assertion style throughout: an invalid ticket is a SUCCESSFUL result whose
/// payload says IsValid=false with a reason code — see TicketValidationResponse for why. Only "not
/// an organizer" and "lost the Redis lock" assert on result.IsFailure.
/// </summary>
public class TicketValidationServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketValidationService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();
    private readonly Guid _scannerUserId = Guid.NewGuid();

    /// <summary>Matches TicketingTestContext.Clock's pinned start — "today" for every test here.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public TicketValidationServiceTests()
    {
        _sut = _fixture.CreateTicketValidationService();
        MockProduct(_productId, TicketingMode.SingleOccurrence, Today.ToDateTime(new TimeOnly(20, 0)));
        MockProduct(_otherProductId, TicketingMode.SingleOccurrence, Today.ToDateTime(new TimeOnly(20, 0)));
    }

    // ---------------------------------------------------------------- ValidateAsync: happy path

    [Fact]
    public async Task ValidateAsync_ForALiveTicketOnTheRightProduct_ReturnsValidAndBurnsTheTicket()
    {
        var ticket = await SeedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.Code.Should().Be("ticket.valid");
        result.Value.TicketId.Should().Be(ticket.Id);
        result.Value.SectorName.Should().Be("VIP");

        // "IT MUST BE INVALIDATED" — the whole point. Re-read from the database, not the in-memory
        // instance, so this proves the write actually committed.
        var persisted = await _fixture.DbContext.Tickets.FindAsync(ticket.Id);
        persisted!.Status.Should().Be(TicketStatus.Used);
        persisted.ValidatedAt.Should().Be(_fixture.Clock.GetUtcNow().UtcDateTime);
        persisted.ValidatedByUserId.Should().Be(_scannerUserId);
    }

    [Fact]
    public async Task ValidateAsync_WithABareTicketGuidTypedByHand_ReturnsValid()
    {
        var ticket = await SeedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, ticket.Id.ToString()), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_AsPlatformStaff_BypassesTheOrganizationCheck()
    {
        var ticket = await SeedTicketAsync(organizationId: _orgB);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), PlatformStaff());

        result.Value!.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------- ValidateAsync: refusal paths

    [Fact]
    public async Task ValidateAsync_ForAnAlreadyUsedTicket_ReturnsInvalidWithTheFirstValidationTime()
    {
        var ticket = await SeedTicketAsync();
        var code = _fixture.QrCodec.Sign(ticket.Id);
        var firstScanAt = _fixture.Clock.GetUtcNow().UtcDateTime;

        await _sut.ValidateAsync(Request(_productId, code), OrganizerOf(_orgA));
        var second = await _sut.ValidateAsync(Request(_productId, code), OrganizerOf(_orgA));

        second.IsSuccess.Should().BeTrue();
        second.Value!.IsValid.Should().BeFalse();
        second.Value.Code.Should().Be("ticket.already_used");
        second.Value.Message.Should().Be("Ulaznica je već iskorištena.");
        // Shown at the gate so staff can see when it was let through, and by implication that this
        // is a duplicate rather than a system error.
        second.Value.ValidatedAt.Should().Be(firstScanAt);
    }

    [Fact]
    public async Task ValidateAsync_ForATicketBelongingToADifferentProduct_ReturnsInvalid()
    {
        // The requirement this feature exists for: a ticket to another event is a perfectly good
        // ticket and still must not open this gate.
        var ticket = await SeedTicketAsync(productId: _otherProductId);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_product");
        result.Value.Message.Should().Be("Ulaznica ne pripada odabranom događaju.");

        (await _fixture.DbContext.Tickets.FindAsync(ticket.Id))!.Status.Should().Be(TicketStatus.Confirmed);
    }

    [Fact]
    public async Task ValidateAsync_ForAnotherOrganizationsTicket_ReturnsInvalid()
    {
        var ticket = await SeedTicketAsync(organizationId: _orgB);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_organization");
    }

    [Fact]
    public async Task ValidateAsync_ForACancelledTicket_ReturnsInvalid()
    {
        var ticket = await SeedTicketAsync(status: TicketStatus.Cancelled);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.cancelled");
    }

    [Fact]
    public async Task ValidateAsync_ForAnUnpaidTicket_ReturnsInvalid()
    {
        var ticket = await SeedTicketAsync(status: TicketStatus.Processing);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_confirmed");
    }

    [Fact]
    public async Task ValidateAsync_ForAnUnknownTicketId_ReturnsInvalid()
    {
        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(Guid.NewGuid())), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_found");
        result.Value.TicketId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("random-text-from-some-other-qr")]
    [InlineData("ETK1.00000000000000000000000000000000.forgedsignature")]
    public async Task ValidateAsync_ForAForgedOrUnrecognizedCode_ReturnsInvalidWithoutTouchingTheDatabase(string code)
    {
        var result = await _sut.ValidateAsync(Request(_productId, code), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.qr_invalid");
        // Rejected before the lock is even attempted — no point serializing on a ticket id that
        // was never real.
        _fixture.ValidationLock.Verify(l => l.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------- ValidateAsync: valid-today, by mode

    [Fact]
    public async Task ValidateAsync_ForADailyEntryTicketDatedToday_ReturnsValid()
    {
        var ticket = await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ForADailyEntryTicketDatedAnotherDay_ReturnsInvalid()
    {
        var ticket = await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today.AddDays(3));

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
        result.Value.Message.Should().Contain("27.08.2026");
    }

    [Fact]
    public async Task ValidateAsync_ForARecurringReservationInsideItsPeriod_ReturnsValid()
    {
        var ticket = await SeedTicketAsync(
            mode: TicketingMode.RecurringReservation, validFrom: Today.AddDays(-10), validTo: Today.AddDays(10));

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ForARecurringReservationPastItsPeriod_ReturnsInvalid()
    {
        var ticket = await SeedTicketAsync(
            mode: TicketingMode.RecurringReservation, validFrom: Today.AddDays(-40), validTo: Today.AddDays(-10));

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
    }

    [Fact]
    public async Task ValidateAsync_ForASingleOccurrenceTicketWhoseShowingIsNotToday_ReturnsInvalid()
    {
        // SingleOccurrence tickets carry no date of their own — the answer comes from Catalog.
        MockProduct(_productId, TicketingMode.SingleOccurrence, Today.AddDays(5).ToDateTime(new TimeOnly(20, 0)));
        var ticket = await SeedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
    }

    [Fact]
    public async Task ValidateAsync_WhenCatalogHasNoDateForTheProduct_ReturnsInvalidRatherThanAdmitting()
    {
        MockProduct(_productId, TicketingMode.SingleOccurrence, date: null);
        var ticket = await SeedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.product_unavailable");
    }

    // -------------------------------------------------------------------- ValidateAsync: locking

    [Fact]
    public async Task ValidateAsync_WhenAnotherScannerHoldsTheLock_FailsWithConflict()
    {
        var ticket = await SeedTicketAsync();
        _fixture.ValidationLock
            .Setup(l => l.TryAcquireAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        // A real failure, not an IsValid=false card: this says nothing about the ticket, only about
        // timing, so the scanner should invite a retry rather than turn the holder away.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.validation_in_progress");
        result.Error.Type.Should().Be(ErrorType.Conflict);

        (await _fixture.DbContext.Tickets.FindAsync(ticket.Id))!.Status.Should().Be(TicketStatus.Confirmed);
    }

    [Fact]
    public async Task ValidateAsync_ReleasesTheLock_OnTheHappyPath()
    {
        var ticket = await SeedTicketAsync();

        await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        _fixture.ValidationLock.Verify(
            l => l.ReleaseAsync(ticket.Id, "test-lock-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ReleasesTheLock_EvenWhenTheTicketIsRejected()
    {
        // The failure exits through a different return statement than the happy path — without the
        // finally block, a rejected scan would leave the gate blocked on that ticket for 10s.
        var ticket = await SeedTicketAsync(status: TicketStatus.Cancelled);

        await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        _fixture.ValidationLock.Verify(
            l => l.ReleaseAsync(ticket.Id, "test-lock-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ReleasesTheLock_EvenWhenTheLookupThrows()
    {
        var ticket = await SeedTicketAsync();
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Catalog je nedostupan."));

        var act = async () => await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        await act.Should().ThrowAsync<HttpRequestException>();
        _fixture.ValidationLock.Verify(
            l => l.ReleaseAsync(ticket.Id, "test-lock-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------ GetProductsForTodayAsync

    [Fact]
    public async Task GetProductsForTodayAsync_ReturnsOnlyTodaysProducts_WithLiveAndValidatedCounts()
    {
        await SeedTicketAsync();
        await SeedTicketAsync();
        var used = await SeedTicketAsync();
        // SeedTicketAsync detaches what it returns, so mark the TRACKED instance — calling
        // MarkValidated on the detached copy would silently save nothing.
        var tracked = await _fixture.DbContext.Tickets.FindAsync(used.Id);
        tracked!.MarkValidated(_scannerUserId, _fixture.Clock.GetUtcNow().UtcDateTime);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value!.Should().ContainSingle().Subject;
        row.ProductId.Should().Be(_productId);
        row.Name.Should().Be("Test proizvod");
        row.TotalToday.Should().Be(3);
        row.ValidatedToday.Should().Be(1);
    }

    [Fact]
    public async Task GetProductsForTodayAsync_ExcludesAProductShowingOnAnotherDay()
    {
        MockProduct(_productId, TicketingMode.SingleOccurrence, Today.AddDays(1).ToDateTime(new TimeOnly(20, 0)));
        await SeedTicketAsync();

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductsForTodayAsync_ExcludesADailyEntryProductWithNoTicketsForToday()
    {
        MockProduct(_productId, TicketingMode.DailyEntry, date: null);
        await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today.AddDays(2));

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductsForTodayAsync_ExcludesOtherOrganizationsProducts()
    {
        await SeedTicketAsync(organizationId: _orgB);

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductsForTodayAsync_AsPlatformStaff_SeesEveryOrganization()
    {
        await SeedTicketAsync(organizationId: _orgB);

        var result = await _sut.GetProductsForTodayAsync(PlatformStaff());

        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProductsForTodayAsync_ExcludesCancelledTicketsFromTheCount()
    {
        await SeedTicketAsync();
        await SeedTicketAsync(status: TicketStatus.Cancelled);

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.Value!.Single().TotalToday.Should().Be(1);
    }

    [Fact]
    public async Task GetProductsForTodayAsync_WithNoTicketsAtAll_ReturnsEmptyWithoutCallingCatalog()
    {
        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _fixture.CatalogClient.Verify(
            c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProductsForTodayAsync_ForAnOrganizerWithNoOrganizationClaim_Fails()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
        };
        var caller = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var result = await _sut.GetProductsForTodayAsync(caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.no_organization");
    }

    // ------------------------------------------------- local business day vs UTC (the 00:00 gate)

    /// <summary>Europe/Sarajevo is UTC+2 in August, so 22:30 UTC is already 00:30 the next day at
    /// the door. Deriving "today" from UTC would still say 24.08. here and turn this holder away —
    /// every night, for the first two hours. See PlatformClock.</summary>
    private static readonly DateTimeOffset JustAfterLocalMidnight = new(2026, 8, 24, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAsync_JustAfterLocalMidnight_AcceptsADailyEntryTicketForTheNewLocalDay()
    {
        MockProduct(_productId, TicketingMode.DailyEntry, date: null);
        var ticket = await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today.AddDays(1));
        _fixture.Clock.SetUtcNow(JustAfterLocalMidnight);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
        (await _fixture.DbContext.Tickets.FindAsync(ticket.Id))!.Status.Should().Be(TicketStatus.Used);
    }

    [Fact]
    public async Task ValidateAsync_JustBeforeLocalMidnight_StillRejectsTomorrowsDailyEntryTicket()
    {
        // The other side of the same boundary: 21:30 UTC is 23:30 local, still today, so a ticket
        // for tomorrow must not open the gate half an hour early.
        MockProduct(_productId, TicketingMode.DailyEntry, date: null);
        var ticket = await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today.AddDays(1));
        _fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 8, 24, 21, 30, 0, TimeSpan.Zero));

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
    }

    [Fact]
    public async Task ValidateAsync_JustAfterLocalMidnight_AcceptsARecurringReservationStartingThatLocalDay()
    {
        var tomorrow = Today.AddDays(1);
        var ticket = await SeedTicketAsync(
            mode: TicketingMode.RecurringReservation, validFrom: tomorrow, validTo: tomorrow.AddMonths(1));
        _fixture.Clock.SetUtcNow(JustAfterLocalMidnight);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_JustAfterLocalMidnight_RejectsASingleOccurrenceTicketForTheDayThatJustEnded()
    {
        // Mirror case for the mode that asks Catalog for the date: last night's concert stops
        // working the moment the local day rolls over, not two hours later when UTC catches up.
        var ticket = await SeedTicketAsync();
        _fixture.Clock.SetUtcNow(JustAfterLocalMidnight);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.not_valid_today");
    }

    [Fact]
    public async Task GetProductsForTodayAsync_JustAfterLocalMidnight_ListsTheNewLocalDaysProduct()
    {
        MockProduct(_productId, TicketingMode.DailyEntry, date: null);
        await SeedTicketAsync(mode: TicketingMode.DailyEntry, validDate: Today.AddDays(1));
        _fixture.Clock.SetUtcNow(JustAfterLocalMidnight);

        var result = await _sut.GetProductsForTodayAsync(OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.ProductId.Should().Be(_productId);
    }

    // ------------------------------------------------------------------------------- test setup

    private static ValidateTicketRequest Request(Guid productId, string code) =>
        new() { ProductId = productId, Code = code };

    // ------------------------------------------------- printed tickets (physical ticket export)

    [Fact]
    public async Task ValidateAsync_ForAPrintedTicket_AdmitsItExactlyLikeAnOnlineOne()
    {
        // The whole promise of the physical-ticket export: paper sold over a counter opens the gate
        // on the strength of its printed QR, with no buyer account behind it.
        var ticket = await SeedPrintedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        (await ReloadAsync(ticket.Id)).Status.Should().Be(TicketStatus.Used);
    }

    [Fact]
    public async Task ValidateAsync_ForAPrintedTicketScannedTwice_RejectsTheSecondScan()
    {
        // A printed sheet can be photocopied. The second copy must not get anyone in.
        var ticket = await SeedPrintedTicketAsync();
        var payload = _fixture.QrCodec.Sign(ticket.Id);
        await _sut.ValidateAsync(Request(_productId, payload), OrganizerOf(_orgA));

        var second = await _sut.ValidateAsync(Request(_productId, payload), OrganizerOf(_orgA));

        second.Value!.IsValid.Should().BeFalse();
        second.Value.Code.Should().Be("ticket.already_used");
    }

    [Fact]
    public async Task ValidateAsync_ForAnotherOrganizationsPrintedTicket_IsRejected()
    {
        var ticket = await SeedPrintedTicketAsync(organizationId: _orgB);

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_organization");
    }

    [Fact]
    public async Task ValidateAsync_ForAPrintedTicket_ReportsNoHolderRatherThanABlankEmail()
    {
        var ticket = await SeedPrintedTicketAsync();

        var result = await _sut.ValidateAsync(Request(_productId, _fixture.QrCodec.Sign(ticket.Id)), OrganizerOf(_orgA));

        result.Value!.HolderEmail.Should().BeNull();
    }

    /// <summary>A ticket minted by the physical-ticket export: no buyer, a stub number, and its
    /// print batch as the order it belongs to.</summary>
    private async Task<Ticket> SeedPrintedTicketAsync(Guid? organizationId = null)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = organizationId ?? _orgA,
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        _fixture.DbContext.Sectors.Add(sector);

        // The batch row has to exist: Ticket.PrintBatchId is a real FK, so a printed ticket can
        // never be orphaned from the export that minted it.
        var batch = new TicketPrintBatch
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = sector.OrganizationId,
            RequestedByUserId = Guid.NewGuid(),
            ProductName = "Test proizvod",
            Status = TicketPrintBatchStatus.Ready,
            TicketCount = 1,
            SerialFrom = 1,
            SerialTo = 1,
            NominalValue = 50,
        };
        _fixture.DbContext.TicketPrintBatches.Add(batch);

        var ticket = Ticket.ForPrint(sector.Id, null, batch.Id, _productId, 50, 1, null);
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

    private void MockProduct(Guid productId, TicketingMode mode, DateTime? date)
    {
        var response = new CatalogProductResponse(
            productId, _orgA, PublishStatus.Published, mode, "Test proizvod", date, City.Sarajevo);

        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(productId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([response]);
    }

    private async Task<Ticket> SeedTicketAsync(
        Guid? productId = null,
        Guid? organizationId = null,
        TicketStatus status = TicketStatus.Confirmed,
        TicketingMode mode = TicketingMode.SingleOccurrence,
        DateOnly? validDate = null,
        DateOnly? validFrom = null,
        DateOnly? validTo = null)
    {
        var resolvedProductId = productId ?? _productId;
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = resolvedProductId,
            OrganizationId = organizationId ?? _orgA,
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = mode,
        };
        _fixture.DbContext.Sectors.Add(sector);

        var ticket = mode switch
        {
            TicketingMode.DailyEntry => Ticket.ForDailyEntry(
                sector.Id, null, Guid.NewGuid(), resolvedProductId, Guid.NewGuid(), "buyer@example.com", 50, validDate),
            TicketingMode.RecurringReservation => Ticket.ForRecurringReservation(
                sector.Id, null, Guid.NewGuid(), resolvedProductId, Guid.NewGuid(), "buyer@example.com", 50,
                SeedSubscription(sector.Id), validFrom!.Value, validTo!.Value),
            _ => Ticket.ForSingleOccurrence(
                sector.Id, null, Guid.NewGuid(), resolvedProductId, Guid.NewGuid(), "buyer@example.com", 50),
        };
        ticket.Status = status;

        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();

        // Detached so the service genuinely re-reads from the database rather than getting handed
        // back the instance this method already tracked.
        _fixture.DbContext.ChangeTracker.Clear();
        return ticket;
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

    private ClaimsPrincipal PlatformStaff()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "SuperAdmin"),
            new(ClaimTypes.Email, "staff@example.com"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
