using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.TicketPrint;

/// <summary>
/// Creating a print batch is a sale, not a report: it mints real gate-valid tickets and draws
/// against the same Redis capacity counter a website purchase does. These tests exist mostly to pin
/// down that half of it — that capacity can never be oversold from the counter, that another
/// organization can never print against your product, and that face values come from the database
/// rather than the request.
/// </summary>
public class TicketPrintServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketPrintService _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _organizerId = Guid.NewGuid();

    public TicketPrintServiceTests()
    {
        _sut = _fixture.CreateTicketPrintService();
        MockProduct(TicketingMode.SingleOccurrence);
        GrantCapacity();
    }

    // ------------------------------------------------------------------------------------ options

    [Fact]
    public async Task GetOptionsAsync_ReturnsPublishedSectorsWithTheirTicketTypes()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        await SeedTicketTypeAsync(sector, "Redovna", 80);
        await SeedTicketTypeAsync(sector, "Počasna", 120);

        var result = await _sut.GetOptionsAsync(_productId, null, Organizer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.CanExport.Should().BeTrue();
        result.Value.Sectors.Should().HaveCount(1);
        result.Value.Sectors[0].TicketTypes.Select(t => t.Name).Should().BeEquivalentTo(["Počasna", "Redovna"]);
    }

    [Fact]
    public async Task GetOptionsAsync_ExcludesDraftSectors()
    {
        await SeedSectorAsync("VIP", capacity: 200, price: 80);
        await SeedSectorAsync("Nacrt", capacity: 50, price: 10, status: PublishStatus.Draft);

        var result = await _sut.GetOptionsAsync(_productId, null, Organizer());

        result.Value!.Sectors.Should().ContainSingle().Which.Name.Should().Be("VIP");
    }

    [Fact]
    public async Task GetOptionsAsync_ReportsRemainingCapacityFromTheLiveCounter()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        _fixture.CapacityLock
            .Setup(l => l.GetRemainingAsync(sector.Id, 200, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(146);

        var result = await _sut.GetOptionsAsync(_productId, null, Organizer());

        result.Value!.Sectors[0].Remaining.Should().Be(146);
        result.Value.Sectors[0].Capacity.Should().Be(200);
    }

    [Fact]
    public async Task GetOptionsAsync_ForRecurringReservation_CannotExportAndSaysWhy()
    {
        MockProduct(TicketingMode.RecurringReservation);
        await SeedSectorAsync("A-12", capacity: 1, price: 60);

        var result = await _sut.GetOptionsAsync(_productId, null, Organizer());

        result.Value!.CanExport.Should().BeFalse();
        result.Value.BlockedReason.Should().Contain("mjesečne rezervacije");
    }

    [Fact]
    public async Task GetOptionsAsync_WithNoPublishedSectors_CannotExportAndSaysWhy()
    {
        var result = await _sut.GetOptionsAsync(_productId, null, Organizer());

        result.Value!.CanExport.Should().BeFalse();
        result.Value.BlockedReason.Should().Contain("objavljen sektor");
    }

    [Fact]
    public async Task GetOptionsAsync_ForAnotherOrganizationsProduct_IsForbidden()
    {
        var result = await _sut.GetOptionsAsync(_productId, null, Organizer(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    // ------------------------------------------------------------------------------------- create

    [Fact]
    public async Task CreateAsync_MintsOneGateValidTicketPerRequestedCopy()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 25)), Organizer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketCount.Should().Be(25);
        result.Value.Status.Should().Be(TicketPrintBatchStatus.Queued);

        var tickets = await _fixture.DbContext.Tickets.AsNoTracking().ToListAsync();
        tickets.Should().HaveCount(25);
        tickets.Should().OnlyContain(t => t.Origin == TicketOrigin.Printed);
        // Confirmed, not Processing: the paper is the proof of sale, so the gate must admit it the
        // moment it is printed.
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Confirmed);
        tickets.Should().OnlyContain(t => t.UserId == null && t.UserEmail == null);
    }

    [Fact]
    public async Task CreateAsync_QueuesTheBatchForRendering()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 3)), Organizer());

        _fixture.PrintQueue.Enqueued.Should().ContainSingle().Which.Should().Be(result.Value!.Id);
    }

    [Fact]
    public async Task CreateAsync_ConfirmsTheCapacityHoldSoTheSeatsAreGoneForOnlineBuyers()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        await _sut.CreateAsync(Request((sector.Id, null, 25)), Organizer());

        _fixture.CapacityLock.Verify(
            l => l.TryHoldAsync(sector.Id, 200, 25, null, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenCapacityIsExhausted_ReturnsConflictAndMintsNothing()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        DenyCapacity(sector.Id, remaining: 146);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 200)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.capacity_exceeded");
        result.Error.Message.Should().Contain("VIP").And.Contain("146");
        _fixture.DbContext.Tickets.Should().BeEmpty();
        _fixture.PrintQueue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenTheSecondSectorHasNoRoom_ReleasesTheFirstSectorsHold()
    {
        // Without the rollback the first sector would stay permanently short by a batch that was
        // never created — capacity silently leaking on every failed export.
        var roomy = await SeedSectorAsync("Standard", capacity: 1200, price: 35);
        var full = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        DenyCapacity(full.Id, remaining: 0);

        var result = await _sut.CreateAsync(Request((roomy.Id, null, 100), (full.Id, null, 50)), Organizer());

        result.IsFailure.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.CapacityLock.Verify(l => l.ConfirmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _fixture.DbContext.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_TakesFaceValuesFromTheDatabaseNotTheRequest()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        var honorary = await SeedTicketTypeAsync(sector, "Počasna", 120);

        var result = await _sut.CreateAsync(Request((sector.Id, honorary.Id, 4)), Organizer());

        result.Value!.NominalValue.Should().Be(480);
        var tickets = await _fixture.DbContext.Tickets.AsNoTracking().ToListAsync();
        tickets.Should().OnlyContain(t => t.PricePaid == 120m);
    }

    [Fact]
    public async Task CreateAsync_NumbersStubsSequentiallyAndContinuesAcrossBatches()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var first = await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer());
        await CompleteAsync(first.Value!.Id);
        var second = await _sut.CreateAsync(Request((sector.Id, null, 3)), Organizer());

        first.Value!.SerialFrom.Should().Be(1);
        first.Value.SerialTo.Should().Be(5);
        second.Value!.SerialFrom.Should().Be(6);
        second.Value.SerialTo.Should().Be(8);

        var serials = await _fixture.DbContext.Tickets.AsNoTracking().Select(t => t.SerialNumber).ToListAsync();
        serials.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateAsync_WhileAnotherBatchIsStillRunning_ReturnsConflict()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer());

        var second = await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("print.batch_in_progress");
    }

    [Fact]
    public async Task CreateAsync_ForADraftSector_ReturnsValidationError()
    {
        var draft = await SeedSectorAsync("Nacrt", capacity: 50, price: 10, status: PublishStatus.Draft);

        var result = await _sut.CreateAsync(Request((draft.Id, null, 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_published");
    }

    [Fact]
    public async Task CreateAsync_ForASectorOnAnotherProduct_ReturnsValidationError()
    {
        var foreign = await SeedSectorAsync("Tuđi", capacity: 50, price: 10, productId: Guid.NewGuid());

        var result = await _sut.CreateAsync(Request((foreign.Id, null, 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.wrong_product");
    }

    [Fact]
    public async Task CreateAsync_ForAnotherOrganizationsProduct_IsForbidden()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
        _fixture.DbContext.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ForPlatformStaff_BypassesTheOrganizationCheck()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 5)), PlatformStaff());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ForRecurringReservation_IsRejected()
    {
        MockProduct(TicketingMode.RecurringReservation);
        var sector = await SeedSectorAsync("A-12", capacity: 1, price: 60);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 1)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.mode_not_supported");
    }

    [Fact]
    public async Task CreateAsync_WhenASectorPricesThroughTicketTypes_RequiresOne()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        await SeedTicketTypeAsync(sector, "Redovna", 80);

        var result = await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("tickettype.required");
    }

    [Fact]
    public async Task CreateAsync_WhenASectorHasNoTicketTypes_RejectsOne()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request((sector.Id, Guid.NewGuid(), 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("tickettype.not_applicable");
    }

    [Fact]
    public async Task CreateAsync_WithATicketTypeFromAnotherSector_ReturnsNotFound()
    {
        var vip = await SeedSectorAsync("VIP", capacity: 200, price: 80);
        await SeedTicketTypeAsync(vip, "Redovna", 80);
        var standard = await SeedSectorAsync("Standard", capacity: 500, price: 35);
        var otherType = await SeedTicketTypeAsync(standard, "Studentska", 20);

        var result = await _sut.CreateAsync(Request((vip.Id, otherType.Id, 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("tickettype.not_found");
    }

    // --------------------------------------------------------------------------------- DailyEntry

    [Fact]
    public async Task CreateAsync_ForDailyEntry_WithoutADate_ReturnsValidationError()
    {
        MockProduct(TicketingMode.DailyEntry);
        var sector = await SeedDailyEntrySectorAsync();

        var result = await _sut.CreateAsync(Request((sector.Id, null, 5)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.date_required");
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntry_StampsTheChosenDayOnEveryTicket()
    {
        MockProduct(TicketingMode.DailyEntry);
        var sector = await SeedDailyEntrySectorAsync();
        var day = new DateOnly(2026, 8, 30);

        var result = await _sut.CreateAsync(Request(day, (sector.Id, null, 4)), Organizer());

        result.IsSuccess.Should().BeTrue();
        var tickets = await _fixture.DbContext.Tickets.AsNoTracking().ToListAsync();
        tickets.Should().OnlyContain(t => t.ValidDate == day);
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntry_WithADayOutsideTheSectorsMonth_ReturnsValidationError()
    {
        MockProduct(TicketingMode.DailyEntry);
        var sector = await SeedDailyEntrySectorAsync();

        var result = await _sut.CreateAsync(Request(new DateOnly(2026, 10, 3), (sector.Id, null, 4)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.date_outside_period");
    }

    [Fact]
    public async Task CreateAsync_ForSingleOccurrence_WithADate_ReturnsValidationError()
    {
        var sector = await SeedSectorAsync("VIP", capacity: 200, price: 80);

        var result = await _sut.CreateAsync(Request(new DateOnly(2026, 8, 30), (sector.Id, null, 4)), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.date_not_applicable");
    }

    // ----------------------------------------------------------------------------------- download

    [Fact]
    public async Task DownloadAsync_HandsOverTheFileAndDestroysTheStoredCopy()
    {
        // The stored sheet is thousands of working gate codes. It exists for exactly one download.
        var batch = await SeedReadyBatchAsync();

        var result = await _sut.DownloadAsync(batch.Id, Organizer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Equal([1, 2, 3]);
        result.Value.FileName.Should().StartWith("ulaznice-");
        _fixture.DbContext.Set<TicketPrintBatchFile>().Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadAsync_ASecondTime_ReturnsFileGone()
    {
        var batch = await SeedReadyBatchAsync();
        await _sut.DownloadAsync(batch.Id, Organizer());

        var second = await _sut.DownloadAsync(batch.Id, Organizer());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("print.file_gone");
    }

    [Fact]
    public async Task DownloadAsync_BeforeTheRenderFinishes_ReturnsConflict()
    {
        var batch = await SeedBatchAsync(TicketPrintBatchStatus.Rendering);

        var result = await _sut.DownloadAsync(batch.Id, Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.not_ready");
    }

    [Fact]
    public async Task DownloadAsync_ForAnotherOrganizationsBatch_IsForbidden()
    {
        var batch = await SeedReadyBatchAsync();

        var result = await _sut.DownloadAsync(batch.Id, Organizer(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.forbidden");
        _fixture.DbContext.Set<TicketPrintBatchFile>().Should().HaveCount(1);
    }

    [Fact]
    public async Task DownloadAsync_MarksTheBatchCollectedSoTheBadgeStopsShowingIt()
    {
        var batch = await SeedReadyBatchAsync();

        await _sut.DownloadAsync(batch.Id, Organizer());

        var outstanding = await _sut.GetOutstandingAsync(Organizer());
        outstanding.Value.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------- retry / listing

    [Fact]
    public async Task RetryAsync_ForAFailedBatch_RequeuesItWithoutMintingMoreTickets()
    {
        var batch = await SeedBatchAsync(TicketPrintBatchStatus.Failed, error: "Generisanje PDF-a nije uspjelo.");

        var result = await _sut.RetryAsync(batch.Id, Organizer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(TicketPrintBatchStatus.Queued);
        result.Value.ErrorMessage.Should().BeNull();
        _fixture.PrintQueue.Enqueued.Should().Contain(batch.Id);
        _fixture.DbContext.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task RetryAsync_ForAReadyBatch_ReturnsConflict()
    {
        var batch = await SeedReadyBatchAsync();

        var result = await _sut.RetryAsync(batch.Id, Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.not_retryable");
    }

    [Fact]
    public async Task GetOutstandingAsync_ForPlatformStaff_ReturnsAnEmptyListRatherThanFailing()
    {
        // Platform staff have no organization of their own, so there is no "my exports" badge for
        // them — but asking must not be an error.
        await SeedReadyBatchAsync();

        var result = await _sut.GetOutstandingAsync(PlatformStaff());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestForProductAsync_WithNoBatchesYet_SucceedsWithNull()
    {
        var result = await _sut.GetLatestForProductAsync(_productId, Organizer());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ForAnUnknownBatch_ReturnsNotFound()
    {
        var result = await _sut.GetAsync(Guid.NewGuid(), Organizer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("print.batch_not_found");
    }

    // ------------------------------------------------------------------------------- test setup

    private void MockProduct(TicketingMode mode) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                _productId, _organizationId, PublishStatus.Published, mode,
                "Ljetni Festival", new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), City.Sarajevo));

    /// <summary>Capacity is available by default; the tests that care about exhaustion narrow it
    /// with <see cref="DenyCapacity"/>.</summary>
    private void GrantCapacity()
    {
        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new HoldResult(true, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(2)));

        _fixture.CapacityLock
            .Setup(l => l.GetRemainingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int capacity, DateOnly? _, CancellationToken _) => capacity);
    }

    private void DenyCapacity(Guid sectorId, int remaining)
    {
        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(sectorId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(false, null, null));

        _fixture.CapacityLock
            .Setup(l => l.GetRemainingAsync(sectorId, It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(remaining);
    }

    private async Task<Sector> SeedSectorAsync(
        string name, int capacity, decimal price,
        PublishStatus status = PublishStatus.Published, Guid? productId = null)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? _productId,
            OrganizationId = _organizationId,
            Name = name,
            Capacity = capacity,
            Price = price,
            Status = status,
            TicketingMode = TicketingMode.SingleOccurrence,
        };

        _fixture.DbContext.Sectors.Add(sector);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return sector;
    }

    private async Task<Sector> SeedDailyEntrySectorAsync()
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = _organizationId,
            Name = "Dnevna",
            Capacity = 300,
            Price = 15,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.DailyEntry,
            PeriodYear = 2026,
            PeriodMonth = 8,
        };

        _fixture.DbContext.Sectors.Add(sector);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return sector;
    }

    private async Task<TicketType> SeedTicketTypeAsync(Sector sector, string name, decimal price)
    {
        var ticketType = new TicketType { Id = Guid.NewGuid(), SectorId = sector.Id, Name = name, Price = price };
        _fixture.DbContext.TicketTypes.Add(ticketType);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return ticketType;
    }

    private async Task<TicketPrintBatch> SeedBatchAsync(TicketPrintBatchStatus status, string? error = null)
    {
        var batch = new TicketPrintBatch
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = _organizationId,
            RequestedByUserId = _organizerId,
            ProductName = "Ljetni Festival",
            Status = status,
            TicketCount = 3,
            SerialFrom = 1,
            SerialTo = 3,
            NominalValue = 240,
            ErrorMessage = error,
            CompletedAt = status is TicketPrintBatchStatus.Ready or TicketPrintBatchStatus.Failed
                ? _fixture.Clock.GetUtcNow().UtcDateTime
                : null,
        };

        _fixture.DbContext.TicketPrintBatches.Add(batch);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return batch;
    }

    private async Task<TicketPrintBatch> SeedReadyBatchAsync()
    {
        var batch = await SeedBatchAsync(TicketPrintBatchStatus.Ready);
        _fixture.DbContext.Set<TicketPrintBatchFile>().Add(new TicketPrintBatchFile
        {
            BatchId = batch.Id,
            Content = [1, 2, 3],
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return batch;
    }

    /// <summary>Takes a batch out of flight so the one-at-a-time guard doesn't block the next
    /// create — the render worker does this for real.</summary>
    private async Task CompleteAsync(Guid batchId)
    {
        var batch = await _fixture.DbContext.TicketPrintBatches.FirstAsync(b => b.Id == batchId);
        batch.Status = TicketPrintBatchStatus.Ready;
        batch.CompletedAt = _fixture.Clock.GetUtcNow().UtcDateTime;
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private CreateTicketPrintBatchRequest Request(params (Guid SectorId, Guid? TicketTypeId, int Quantity)[] lines)
        => Request(null, lines);

    private CreateTicketPrintBatchRequest Request(
        DateOnly? validDate, params (Guid SectorId, Guid? TicketTypeId, int Quantity)[] lines) => new()
        {
            ProductId = _productId,
            ValidDate = validDate,
            Lines = [.. lines.Select(l => new TicketPrintLineRequest
            {
                SectorId = l.SectorId,
                TicketTypeId = l.TicketTypeId,
                Quantity = l.Quantity,
            })],
        };

    private ClaimsPrincipal Organizer(Guid? organizationId = null) =>
        Principal(_organizerId, "OrganizationAdmin", organizationId ?? _organizationId);

    private static ClaimsPrincipal PlatformStaff() => Principal(Guid.NewGuid(), "Admin", null);

    private static ClaimsPrincipal Principal(Guid userId, string role, Guid? organizationId)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, "organizer@example.com"),
        ];

        if (organizationId is { } id) claims.Add(new Claim("organizationId", id.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
