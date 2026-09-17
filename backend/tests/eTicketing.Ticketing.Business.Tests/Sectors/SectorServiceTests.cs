using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Sectors;

public class SectorServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ISectorService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _singleOccurrenceProductId = Guid.NewGuid();
    private readonly Guid _dailyEntryProductId = Guid.NewGuid();
    private readonly Guid _recurringReservationProductId = Guid.NewGuid();

    public SectorServiceTests()
    {
        _sut = _fixture.CreateSectorService();

        MockProduct(_singleOccurrenceProductId, _orgA, TicketingMode.SingleOccurrence);
        MockProduct(_dailyEntryProductId, _orgA, TicketingMode.DailyEntry);
        MockProduct(_recurringReservationProductId, _orgA, TicketingMode.RecurringReservation);

        // Nothing sold, so every sector reads back at full capacity. Without this the loose mock
        // returns 0 and GetPublishedAsync would report every sector as sold out, which is the one
        // answer that changes what a client renders — the availability tests below override it.
        MockRemaining((_, capacity) => capacity);
    }

    /// <summary>Points ISectorCapacityLock.GetRemainingAsync at a function of (sectorId, capacity)
    /// so a test can say "this sector is exhausted" without restating the whole mock.</summary>
    private void MockRemaining(Func<Guid, int, int> remaining) =>
        _fixture.CapacityLock
            .Setup(l => l.GetRemainingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid sectorId, int capacity, DateOnly? _, CancellationToken _) => remaining(sectorId, capacity));

    private void MockProduct(Guid productId, Guid organizationId, TicketingMode mode) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(productId, organizationId, PublishStatus.Published, mode, "Test proizvod", null, City.Sarajevo));

    private static ClaimsPrincipal BuildCaller(string role, Guid? organizationId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };
        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>A plain buyer whose user id the test chooses, so it can assert which account a hold
    /// was recorded against — BuildCaller mints a fresh NameIdentifier on every call.</summary>
    private static ClaimsPrincipal BuyerCaller(Guid userId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "User")],
            "TestAuth"));

    private ClaimsPrincipal OrgACaller() => BuildCaller("OrganizationSuperAdmin", _orgA);
    private ClaimsPrincipal OrgBCaller() => BuildCaller("OrganizationSuperAdmin", _orgB);
    private static ClaimsPrincipal PlatformStaffCaller() => BuildCaller("SuperAdmin");

    private UpsertSectorRequest SingleOccurrenceRequest() => new()
    {
        ProductId = _singleOccurrenceProductId,
        Name = "VIP",
        Capacity = 100,
        Price = 50,
    };

    private UpsertSectorRequest DailyEntryRequest(int? year = 2026, int? month = 8) => new()
    {
        ProductId = _dailyEntryProductId,
        Name = "August 2026",
        Capacity = 300,
        Price = 10,
        PeriodYear = year,
        PeriodMonth = month,
    };

    private UpsertSectorRequest RecurringReservationRequest(int capacity = 1) => new()
    {
        ProductId = _recurringReservationProductId,
        Name = "A-12",
        Capacity = capacity,
        Price = 80,
    };

    // --- CreateAsync: mode-conditional validation ---

    [Fact]
    public async Task CreateAsync_ForSingleOccurrence_HappyPath_Succeeds()
    {
        var result = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketingMode.Should().Be(TicketingMode.SingleOccurrence);
        result.Value.Status.Should().Be(PublishStatus.Draft);
    }

    [Fact]
    public async Task CreateAsync_ForSingleOccurrence_WithPeriodFields_ReturnsValidationError()
    {
        var request = SingleOccurrenceRequest() with { PeriodYear = 2026, PeriodMonth = 8 };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.period_not_applicable");
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntry_HappyPath_Succeeds()
    {
        var result = await _sut.CreateAsync(DailyEntryRequest(), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.PeriodYear.Should().Be(2026);
        result.Value.PeriodMonth.Should().Be(8);
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntry_WithoutPeriod_ReturnsValidationError()
    {
        var request = DailyEntryRequest(year: null, month: null);

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.period_required");
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntry_WithInvalidMonth_ReturnsValidationError()
    {
        var request = DailyEntryRequest(month: 13);

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.period_invalid_month");
    }

    [Fact]
    public async Task CreateAsync_ForRecurringReservation_HappyPath_Succeeds()
    {
        var result = await _sut.CreateAsync(RecurringReservationRequest(), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Capacity.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ForRecurringReservation_WithCapacityOtherThanOne_ReturnsValidationError()
    {
        var request = RecurringReservationRequest(capacity: 5);

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.capacity_must_be_one");
    }

    [Fact]
    public async Task CreateAsync_ForUnknownProduct_ReturnsValidationError()
    {
        var request = SingleOccurrenceRequest() with { ProductId = Guid.NewGuid() };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_not_found");
    }

    [Fact]
    public async Task CreateAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var result = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.forbidden");
    }

    [Fact]
    public async Task CreateAsync_ForAnotherOrganizationsProduct_ByPlatformStaff_Succeeds()
    {
        var result = await _sut.CreateAsync(SingleOccurrenceRequest(), PlatformStaffCaller());

        result.IsSuccess.Should().BeTrue();
    }

    // --- PreviewAsync ---

    [Fact]
    public async Task PreviewAsync_DoesNotPersist()
    {
        var before = await _sut.GetAllAsync(new SectorQuery());

        await _sut.PreviewAsync(SingleOccurrenceRequest(), OrgACaller());

        var after = await _sut.GetAllAsync(new SectorQuery());
        after.Value!.TotalCount.Should().Be(before.Value!.TotalCount);
    }

    [Fact]
    public async Task PreviewAsync_UsesSameValidationAsCreateAsync()
    {
        var request = DailyEntryRequest(year: null, month: null);

        var result = await _sut.PreviewAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.period_required");
    }

    // --- PublishAsync / UpdateAsync / DeleteAsync ---

    [Fact]
    public async Task PublishAsync_ForOwnDraft_SetsStatusToPublished()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        var result = await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_ForAnotherOrganizationsSector_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        var result = await _sut.PublishAsync(created.Value!.Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.forbidden");
    }

    [Fact]
    public async Task PublishAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.PublishAsync(Guid.NewGuid(), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
    }

    [Fact]
    public async Task UpdateAsync_ForPublishedSector_KeepsItPublished()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        var updated = await _sut.UpdateAsync(created.Value.Id, SingleOccurrenceRequest() with { Name = "Regular" }, OrgACaller());

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Status.Should().Be(PublishStatus.Published);
        updated.Value.Name.Should().Be("Regular");
    }

    [Fact]
    public async Task UpdateAsync_WhenProductIdChanges_ReturnsValidationError()
    {
        // Regression test for PR #19 review feedback: ProductId is immutable after creation —
        // letting it change would silently re-parent OrganizationId/TicketingMode and could
        // invalidate outstanding holds' semantics once ticket purchasing exists.
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        var result = await _sut.UpdateAsync(
            created.Value!.Id, SingleOccurrenceRequest() with { ProductId = _dailyEntryProductId }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_id_immutable");
    }

    [Fact]
    public async Task DeleteAsync_ForOwnSector_RemovesIt()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        var result = await _sut.DeleteAsync(created.Value!.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        var all = await _sut.GetAllAsync(new SectorQuery());
        all.Value!.Items.Should().NotContain(s => s.Id == created.Value.Id);
    }

    [Fact]
    public async Task DeleteAsync_ForAnotherOrganizationsSector_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());

        var result = await _sut.DeleteAsync(created.Value!.Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.forbidden");
    }

    // --- GetPublishedAsync / GetMineAsync ---

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublished()
    {
        var draft = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        var published = await _sut.CreateAsync(SingleOccurrenceRequest() with { Name = "Published Sector" }, OrgACaller());
        await _sut.PublishAsync(published.Value!.Id, OrgACaller());

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _singleOccurrenceProductId });

        result.Value!.Items.Should().ContainSingle(s => s.Id == published.Value.Id);
        result.Value.Items.Should().NotContain(s => s.Id == draft.Value!.Id);
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallersOrganization()
    {
        await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.CreateAsync(SingleOccurrenceRequest(), PlatformStaffCaller()); // still lands in _orgA per mocked product

        var result = await _sut.GetMineAsync(new SectorQuery(), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2); // both land in _orgA per the mocked product's OrganizationId
    }

    // --- GetPublishedAsync: live availability (RemainingCapacity / IsSoldOut) ---

    [Fact]
    public async Task GetPublishedAsync_ReportsRemainingCapacityFromTheLiveCounter()
    {
        var published = await CreatePublishedSingleOccurrenceAsync();
        MockRemaining((_, _) => 37);

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _singleOccurrenceProductId });

        var sector = result.Value!.Items.Single(s => s.Id == published);
        sector.RemainingCapacity.Should().Be(37);
        sector.IsSoldOut.Should().BeFalse();
    }

    [Fact]
    public async Task GetPublishedAsync_WhenCounterIsExhausted_MarksSectorSoldOut()
    {
        var published = await CreatePublishedSingleOccurrenceAsync();
        MockRemaining((_, _) => 0);

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _singleOccurrenceProductId });

        var sector = result.Value!.Items.Single(s => s.Id == published);
        sector.RemainingCapacity.Should().Be(0);
        sector.IsSoldOut.Should().BeTrue();
    }

    [Fact]
    public async Task GetPublishedAsync_ReportsPerSectorAvailability_NotOneNumberForAll()
    {
        // The whole point of the field: a buyer must be able to see that VIP is gone while Parter
        // is still on sale, on the same event.
        var soldOut = await CreatePublishedSingleOccurrenceAsync("VIP");
        var onSale = await CreatePublishedSingleOccurrenceAsync("Parter");
        MockRemaining((sectorId, _) => sectorId == soldOut ? 0 : 12);

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _singleOccurrenceProductId });

        result.Value!.Items.Single(s => s.Id == soldOut).IsSoldOut.Should().BeTrue();
        result.Value.Items.Single(s => s.Id == onSale).IsSoldOut.Should().BeFalse();
    }

    [Fact]
    public async Task GetPublishedAsync_ForDailyEntry_WithoutDate_LeavesRemainingCapacityUnknown()
    {
        // DailyEntry counts capacity per (Sector, date). Any single number would be a lie, and a
        // zero would wrongly grey out a sector that has seats on every other day of the month.
        var published = await CreatePublishedDailyEntryAsync();
        MockRemaining((_, _) => 0);

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _dailyEntryProductId });

        var sector = result.Value!.Items.Single(s => s.Id == published);
        sector.RemainingCapacity.Should().BeNull();
        sector.IsSoldOut.Should().BeFalse();
    }

    [Fact]
    public async Task GetPublishedAsync_ForDailyEntry_WithDateInPeriod_ReportsThatDaysRemaining()
    {
        var published = await CreatePublishedDailyEntryAsync(); // August 2026
        var date = new DateOnly(2026, 8, 15);
        MockRemaining((_, _) => 5);

        var result = await _sut.GetPublishedAsync(
            new SectorQuery { ProductId = _dailyEntryProductId, Date = date });

        result.Value!.Items.Single(s => s.Id == published).RemainingCapacity.Should().Be(5);
        _fixture.CapacityLock.Verify(
            l => l.GetRemainingAsync(published, 300, date, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPublishedAsync_ForDailyEntry_WithDateOutsidePeriod_LeavesRemainingCapacityUnknown()
    {
        // Reading September's counter for an August sector would report some other month's sales
        // as this one's — better to say nothing than to say something wrong.
        var published = await CreatePublishedDailyEntryAsync(); // August 2026
        MockRemaining((_, _) => 0);

        var result = await _sut.GetPublishedAsync(
            new SectorQuery { ProductId = _dailyEntryProductId, Date = new DateOnly(2026, 9, 1) });

        result.Value!.Items.Single(s => s.Id == published).RemainingCapacity.Should().BeNull();
    }

    [Fact]
    public async Task GetPublishedAsync_ForSingleOccurrence_IgnoresTheDateParameter()
    {
        // Date is a DailyEntry concept. Every other mode counts per sector, so the counter must be
        // read without a date or it would resolve to an empty per-day key and report full capacity.
        var published = await CreatePublishedSingleOccurrenceAsync();

        await _sut.GetPublishedAsync(
            new SectorQuery { ProductId = _singleOccurrenceProductId, Date = new DateOnly(2026, 8, 15) });

        _fixture.CapacityLock.Verify(
            l => l.GetRemainingAsync(published, 100, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPublishedAsync_ForRecurringReservation_WhenSpaceIsTaken_MarksItSoldOut()
    {
        // Capacity is always 1 for a parking space, so "sold out" here means somebody has it.
        var created = await _sut.CreateAsync(RecurringReservationRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());
        MockRemaining((_, _) => 0);

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _recurringReservationProductId });

        result.Value!.Items.Single(s => s.Id == created.Value.Id).IsSoldOut.Should().BeTrue();
    }

    [Fact]
    public async Task GetMineAsync_LeavesRemainingCapacityUnknown()
    {
        // The organizer list deliberately doesn't pay a Redis round-trip per row for a number its
        // screens never render — availability is a buying-side concern.
        await CreatePublishedSingleOccurrenceAsync();

        var result = await _sut.GetMineAsync(new SectorQuery(), OrgACaller());

        result.Value!.Items.Should().OnlyContain(s => s.RemainingCapacity == null);
        _fixture.CapacityLock.Verify(
            l => l.GetRemainingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_LeavesRemainingCapacityUnknown()
    {
        await CreatePublishedSingleOccurrenceAsync();

        var result = await _sut.GetAllAsync(new SectorQuery());

        result.Value!.Items.Should().OnlyContain(s => s.RemainingCapacity == null);
    }

    [Fact]
    public async Task GetPublishedAsync_DoesNotReportDraftSectorsAvailability()
    {
        // A draft sector isn't on sale at all, so it must not appear — sold out or otherwise.
        var draft = await _sut.CreateAsync(SingleOccurrenceRequest() with { Name = "Nacrt" }, OrgACaller());

        var result = await _sut.GetPublishedAsync(new SectorQuery { ProductId = _singleOccurrenceProductId });

        result.Value!.Items.Should().NotContain(s => s.Id == draft.Value!.Id);
    }

    private async Task<Guid> CreatePublishedSingleOccurrenceAsync(string name = "VIP")
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest() with { Name = name }, OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());
        return created.Value.Id;
    }

    private async Task<Guid> CreatePublishedDailyEntryAsync()
    {
        var created = await _sut.CreateAsync(DailyEntryRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());
        return created.Value.Id;
    }

    // --- HoldAsync ---

    [Fact]
    public async Task HoldAsync_ForPublishedSingleOccurrenceSector_CallsCapacityLockWithoutDate()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(created.Value.Id, 100, 2, null, It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(true, "hold-1", DateTime.UtcNow.AddMinutes(5)));

        var result = await _sut.HoldAsync(created.Value.Id, new HoldSectorRequest { Quantity = 2 }, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.HoldId.Should().Be("hold-1");
    }

    [Fact]
    public async Task HoldAsync_ForDailyEntrySector_WithoutDate_ReturnsValidationError()
    {
        var created = await _sut.CreateAsync(DailyEntryRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        var result = await _sut.HoldAsync(created.Value.Id, new HoldSectorRequest { Quantity = 1 }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.date_required");
    }

    [Fact]
    public async Task HoldAsync_ForDailyEntrySector_WithDateOutsidePeriod_ReturnsValidationError()
    {
        var created = await _sut.CreateAsync(DailyEntryRequest(), OrgACaller()); // August 2026
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        var result = await _sut.HoldAsync(
            created.Value.Id, new HoldSectorRequest { Quantity = 1, Date = new DateOnly(2026, 9, 1) }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.date_out_of_period");
    }

    [Fact]
    public async Task HoldAsync_ForDailyEntrySector_WithDateWithinPeriod_Succeeds()
    {
        var created = await _sut.CreateAsync(DailyEntryRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());
        var date = new DateOnly(2026, 8, 15);

        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(created.Value.Id, 300, 1, date, It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(true, "hold-2", DateTime.UtcNow.AddMinutes(5)));

        var result = await _sut.HoldAsync(created.Value.Id, new HoldSectorRequest { Quantity = 1, Date = date }, OrgACaller());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HoldAsync_WhenCapacityLockReportsNoCapacity_ReturnsConflict()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(created.Value.Id, 100, 999, null, It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(false, null, null));

        var result = await _sut.HoldAsync(created.Value.Id, new HoldSectorRequest { Quantity = 999 }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.no_capacity");
    }

    [Fact]
    public async Task HoldAsync_ForUnpublishedSector_ReturnsValidationError()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller()); // still Draft

        var result = await _sut.HoldAsync(created.Value!.Id, new HoldSectorRequest { Quantity = 1 }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_published");
    }

    [Fact]
    public async Task HoldAsync_ForUnknownSector_ReturnsNotFound()
    {
        var result = await _sut.HoldAsync(Guid.NewGuid(), new HoldSectorRequest { Quantity = 1 }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
    }

    [Fact]
    public async Task HoldAsync_RecordsTheCallingAccountAsTheHoldOwner()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());
        var buyerId = Guid.NewGuid();

        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(
                created.Value.Id, 100, 1, null, It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(true, "hold-1", DateTime.UtcNow.AddMinutes(5)));

        await _sut.HoldAsync(created.Value.Id, new HoldSectorRequest { Quantity = 1 }, BuyerCaller(buyerId));

        // The owner is the whole point: without it a leaked hold id is enough for anyone to release
        // or spend this reservation.
        _fixture.CapacityLock.Verify(
            l => l.TryHoldAsync(created.Value.Id, 100, 1, null, It.IsAny<TimeSpan>(), buyerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- ReleaseHoldAsync ---

    /// <summary>Points PeekAsync at a hold owned by <paramref name="ownerId"/> (null = a hold with
    /// no recorded owner: a system hold, or one minted before the owner was recorded).</summary>
    private void MockPeek(string holdId, Guid? ownerId) =>
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync(holdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeldReservation(Guid.NewGuid(), null, 1, ownerId));

    [Fact]
    public async Task ReleaseHoldAsync_ForOwnHold_CallsCapacityLockReleaseAndSucceeds()
    {
        var buyerId = Guid.NewGuid();
        MockPeek("hold-1", buyerId);

        var result = await _sut.ReleaseHoldAsync("hold-1", BuyerCaller(buyerId));

        result.IsSuccess.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync("hold-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseHoldAsync_ForAnotherAccountsHold_DoesNotRelease_ButStillReportsSuccess()
    {
        MockPeek("hold-1", Guid.NewGuid());

        var result = await _sut.ReleaseHoldAsync("hold-1", BuyerCaller(Guid.NewGuid()));

        // Success, not 403: a distinct answer would confirm to whoever presented the id that it
        // names a live hold. What matters is that the victim's capacity is not handed back.
        result.IsSuccess.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseHoldAsync_ForHoldWithNoRecordedOwner_StillReleases()
    {
        // A hold minted by the previous deploy, before the owner was stored. Those keep arriving
        // for the remaining five minutes of their TTL and must stay releasable, or a buyer who
        // held a seat across the deploy cannot give it back — see RedisSectorCapacityLock's
        // HoldInfoVersionPrefix.
        MockPeek("legacy-hold", null);

        var result = await _sut.ReleaseHoldAsync("legacy-hold", BuyerCaller(Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync("legacy-hold", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseHoldAsync_ForUnknownHoldId_StillSucceeds()
    {
        // Mirrors ISectorCapacityLock.ReleaseAsync's own no-op-on-unknown-holdId semantics — the
        // caller only ever wants "make sure this hold isn't holding capacity any more", never
        // confirmation that it existed in the first place. The loose mock's PeekAsync returns null.
        var result = await _sut.ReleaseHoldAsync(Guid.NewGuid().ToString("N"), BuyerCaller(Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        _fixture.CapacityLock.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _fixture.Dispose();
}
