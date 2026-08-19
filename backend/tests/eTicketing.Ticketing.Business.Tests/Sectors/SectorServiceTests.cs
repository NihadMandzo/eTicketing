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
    }

    private void MockProduct(Guid productId, Guid organizationId, TicketingMode mode) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(productId, organizationId, PublishStatus.Published, mode));

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

    // --- HoldAsync ---

    [Fact]
    public async Task HoldAsync_ForPublishedSingleOccurrenceSector_CallsCapacityLockWithoutDate()
    {
        var created = await _sut.CreateAsync(SingleOccurrenceRequest(), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(created.Value.Id, 100, 2, null, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
            .Setup(l => l.TryHoldAsync(created.Value.Id, 300, 1, date, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
            .Setup(l => l.TryHoldAsync(created.Value.Id, 100, 999, null, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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

    public void Dispose() => _fixture.Dispose();
}
