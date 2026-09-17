using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.ReadModels;

/// <summary>
/// What the ProductSnapshot read model is actually <i>for</i>: a sector is only on sale while the
/// product it belongs to is on sale.
///
/// <para>Before this, Sector.Status was the whole answer — and it is written in exactly one place in
/// this service (SectorService.PublishAsync) and never by any consumer. So unpublishing a product in
/// eTicketing.Catalog, or deleting it outright, left its sectors selling tickets indefinitely. These
/// tests are the ones that fail if that regresses.</para>
/// </summary>
public class ProductAvailabilityGateTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ISectorService _sectors;
    private readonly IPurchaseService _purchases;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProductAvailabilityGateTests()
    {
        _sectors = _fixture.CreateSectorService();
        _purchases = _fixture.CreatePurchaseService();

        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                _productId, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Ljetni Festival", new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), City.Mostar));

        _fixture.CapacityLock
            .Setup(l => l.GetRemainingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int capacity, DateOnly? _, CancellationToken _) => capacity);
    }

    private ClaimsPrincipal Caller(string role = "OrganizationSuperAdmin") =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _callerId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Email, "buyer@example.com"),
            new Claim("organizationId", _orgA.ToString()),
        ], "TestAuth"));

    /// <summary>A published sector for the published product. Creating it through the service also
    /// fills the product's snapshot from the mocked Catalog read — which is itself the opportunistic
    /// fill this phase added, exercised here on every single test in this class.</summary>
    private async Task<Sector> CreatePublishedSectorAsync()
    {
        var created = await _sectors.CreateAsync(
            new UpsertSectorRequest { ProductId = _productId, Name = "VIP", Capacity = 100, Price = 50 },
            Caller());
        await _sectors.PublishAsync(created.Value!.Id, Caller());
        return (await _fixture.SectorRepository.GetByIdWithTicketTypesAsync(created.Value.Id))!;
    }

    private async Task SetProductStatusAsync(PublishStatus status)
    {
        var snapshot = await _fixture.DbContext.ProductSnapshots.SingleAsync(p => p.ProductId == _productId);
        snapshot.Status = status;
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    /// <summary>What <c>product.deleted</c> leaves behind: no row at all.</summary>
    private async Task DeleteProductAsync()
    {
        await _fixture.CreateProductSnapshotProjector().RemoveAsync(_productId);
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Creating a sector fills the snapshot ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ForAProductWithNoSnapshotYet_RecordsOneFromTheCatalogReadItAlreadyMade()
    {
        await _sectors.CreateAsync(
            new UpsertSectorRequest { ProductId = _productId, Name = "VIP", Capacity = 100, Price = 50 },
            Caller());

        var snapshot = await _fixture.DbContext.ProductSnapshots.AsNoTracking().SingleAsync();
        snapshot.ProductId.Should().Be(_productId);
        snapshot.Status.Should().Be(PublishStatus.Published);
        snapshot.Name.Should().Be("Ljetni Festival");

        // Free: ValidateAsync had to fetch the product anyway to check ownership and ticketing mode.
        _fixture.CatalogClient.Verify(
            c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewAsync_WritesNoSnapshot()
    {
        // Preview is stateless by contract (.claude/rules/01-domain.md, R34) — filling the read
        // model from it would be a DB write on an endpoint that promises none.
        await _sectors.PreviewAsync(
            new UpsertSectorRequest { ProductId = _productId, Name = "VIP", Capacity = 100, Price = 50 },
            Caller());

        (await _fixture.DbContext.ProductSnapshots.AsNoTracking().CountAsync()).Should().Be(0);
    }

    // ── Listing ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublishedAsync_ForAPublishedProduct_ListsTheSector()
    {
        await CreatePublishedSectorAsync();

        var result = await _sectors.GetPublishedAsync(new SectorQuery { ProductId = _productId });

        result.Value!.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPublishedAsync_ForAnUnpublishedProduct_ListsNothing()
    {
        await CreatePublishedSectorAsync();
        await SetProductStatusAsync(PublishStatus.Draft);

        var result = await _sectors.GetPublishedAsync(new SectorQuery { ProductId = _productId });

        result.Value!.Items.Should().BeEmpty();
        // TotalCount too, not just the page: the filter runs in the query, so a client paging
        // through cannot be told there are results on a page that will come back empty.
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPublishedAsync_ForADeletedProduct_ListsNothing()
    {
        await CreatePublishedSectorAsync();
        await DeleteProductAsync();

        var result = await _sectors.GetPublishedAsync(new SectorQuery { ProductId = _productId });

        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMineAsync_ForAnUnpublishedProduct_StillShowsTheOrganizerTheirSectors()
    {
        // The back office must not go dark. Building sectors before publishing the product is the
        // normal order of work, and an organizer who cannot see what they configured would have no
        // way to fix it.
        await CreatePublishedSectorAsync();
        await SetProductStatusAsync(PublishStatus.Draft);

        var result = await _sectors.GetMineAsync(new SectorQuery { ProductId = _productId }, Caller());

        result.Value!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_ForAnUnpublishedProduct_StillShowsPlatformStaffTheSectors()
    {
        await CreatePublishedSectorAsync();
        await SetProductStatusAsync(PublishStatus.Draft);

        var result = await _sectors.GetAllAsync(new SectorQuery { ProductId = _productId });

        result.Value!.Items.Should().ContainSingle();
    }

    // ── Holding ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HoldAsync_ForAnUnpublishedProduct_IsRefused()
    {
        var sector = await CreatePublishedSectorAsync();
        await SetProductStatusAsync(PublishStatus.Draft);

        var result = await _sectors.HoldAsync(sector.Id, new HoldSectorRequest { Quantity = 1 }, Caller("User"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_unavailable");
        // No capacity touched: refusing after the decrement would strand a seat for the full TTL.
        _fixture.CapacityLock.Verify(
            l => l.TryHoldAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(),
                It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HoldAsync_ForADeletedProduct_IsRefused()
    {
        var sector = await CreatePublishedSectorAsync();
        await DeleteProductAsync();

        var result = await _sectors.HoldAsync(sector.Id, new HoldSectorRequest { Quantity = 1 }, Caller("User"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_unavailable");
    }

    [Fact]
    public async Task HoldAsync_ForAPublishedProduct_Succeeds()
    {
        var sector = await CreatePublishedSectorAsync();
        _fixture.CapacityLock
            .Setup(l => l.TryHoldAsync(sector.Id, 100, 1, null, It.IsAny<TimeSpan>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HoldResult(true, "hold-1", DateTime.UtcNow.AddMinutes(5)));

        var result = await _sectors.HoldAsync(sector.Id, new HoldSectorRequest { Quantity = 1 }, Caller("User"));

        result.IsSuccess.Should().BeTrue();
    }

    // ── Purchasing ───────────────────────────────────────────────────────────────────────────

    private void MockSuccessfulCharge() =>
        _fixture.PaymentClient
            .Setup(p => p.CapturePaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string orderRef, Guid _, decimal amount, string? _, CancellationToken _) =>
                new PaymentChargeResponse(Guid.NewGuid(), amount, PaymentChargeStatus.Succeeded, orderRef, "eur", null));

    private PurchaseRequest Request() => new()
    {
        HoldId = "hold-1",
        LineItems = [new PurchaseLineItemRequest { Quantity = 1 }],
        OrderId = Guid.NewGuid(),
        PaymentIntentId = "pi_test_123",
    };

    [Fact]
    public async Task PurchaseAsync_WhenTheProductIsDeletedWhileTheHoldIsLive_IsRefusedAndNothingIsCharged()
    {
        // The five-minute window the hold buys the buyer is also five minutes in which the organizer
        // can take the product down. Held capacity is not a right to buy something withdrawn from
        // sale.
        var sector = await CreatePublishedSectorAsync();
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeldReservation(sector.Id, null, 1, _callerId));
        MockSuccessfulCharge();
        await DeleteProductAsync();

        var result = await _purchases.PurchaseAsync(Request(), Caller("User"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_unavailable");
        _fixture.PaymentClient.Verify(
            p => p.CapturePaymentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await _fixture.DbContext.Tickets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ForAnUnpublishedProduct_IsRefusedBeforeTheProviderIsCalled()
    {
        var sector = await CreatePublishedSectorAsync();
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeldReservation(sector.Id, null, 1, _callerId));
        await SetProductStatusAsync(PublishStatus.Draft);

        var result = await _purchases.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest { HoldId = "hold-1", LineItems = [new PurchaseLineItemRequest { Quantity = 1 }] },
            Caller("User"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.product_unavailable");
    }

    [Fact]
    public async Task PurchaseAsync_StampsTheProductNameDateAndCityOntoTheEvent()
    {
        // The other half of this phase: eTicketing.PdfGeneration no longer calls Catalog, so if
        // these three do not arrive on the event, every ticket PDF prints a generic heading.
        var sector = await CreatePublishedSectorAsync();
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeldReservation(sector.Id, null, 1, _callerId));
        MockSuccessfulCharge();

        TicketPurchased? published = null;
        _fixture.EventPublisher
            .Setup(p => p.PublishAsync(EventNames.TicketPurchased, It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .Callback<string, TicketPurchased, CancellationToken>((_, e, _) => published = e);

        var result = await _purchases.PurchaseAsync(Request(), Caller("User"));

        result.IsSuccess.Should().BeTrue();
        published.Should().NotBeNull();
        published!.ProductName.Should().Be("Ljetni Festival");
        published.ProductDate.Should().Be(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
        published.ProductCity.Should().Be(City.Mostar);
    }

    [Fact]
    public async Task PurchaseAsync_TakesTheProductNameAsOfPurchase_NotAsOfRendering()
    {
        // A ticket should say what was bought. If the organizer renames the product next week, the
        // PDF in the buyer's inbox must not quietly start describing a different event.
        var sector = await CreatePublishedSectorAsync();
        _fixture.CapacityLock
            .Setup(l => l.PeekAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeldReservation(sector.Id, null, 1, _callerId));
        MockSuccessfulCharge();

        var snapshot = await _fixture.DbContext.ProductSnapshots.SingleAsync(p => p.ProductId == _productId);
        snapshot.Name = "Ljetni Festival 2026 — drugi dan";
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        TicketPurchased? published = null;
        _fixture.EventPublisher
            .Setup(p => p.PublishAsync(EventNames.TicketPurchased, It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .Callback<string, TicketPurchased, CancellationToken>((_, e, _) => published = e);

        await _purchases.PurchaseAsync(Request(), Caller("User"));

        published!.ProductName.Should().Be("Ljetni Festival 2026 — drugi dan");
    }

    public void Dispose() => _fixture.Dispose();
}
