using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.ReadModels;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.ReadModels;

/// <summary>
/// The projection itself: what the ProductSnapshot table looks like after a stream of events that
/// may arrive twice, out of order, or not at all.
///
/// This matters more than a projection usually would, because this table is now a gate on selling.
/// A row that says Draft when the product is Published takes a live event off sale; a row that says
/// Published when the product is deleted keeps taking money for something that no longer exists.
/// </summary>
public class ProductSnapshotProjectorTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IProductSnapshotProjector _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private static readonly DateTime Noon = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    public ProductSnapshotProjectorTests()
    {
        _sut = _fixture.CreateProductSnapshotProjector();
    }

    private ProductSnapshotChanged Event(
        PublishStatus status = PublishStatus.Published,
        string name = "Ljetni Festival",
        DateTime? changedAt = null,
        DateTime? date = null,
        City city = City.Sarajevo) =>
        new(_productId, _organizationId, name, date, city, status,
            TicketingMode.SingleOccurrence, changedAt ?? Noon);

    private Task<ProductSnapshot?> StoredAsync() =>
        _fixture.DbContext.ProductSnapshots.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == _productId);

    [Fact]
    public async Task ApplyAsync_ForAnUnknownProduct_InsertsTheRow()
    {
        await _sut.ApplyAsync(Event(name: "Ljetni Festival", date: new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc)));

        var stored = await StoredAsync();
        stored.Should().NotBeNull();
        stored!.OrganizationId.Should().Be(_organizationId);
        stored.Name.Should().Be("Ljetni Festival");
        stored.Date.Should().Be(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
        stored.City.Should().Be(City.Sarajevo);
        stored.Status.Should().Be(PublishStatus.Published);
        stored.TicketingMode.Should().Be(TicketingMode.SingleOccurrence);
        stored.ChangedAt.Should().Be(Noon);
    }

    [Fact]
    public async Task ApplyAsync_ForAKnownProduct_OverwritesRatherThanInserting()
    {
        await _sut.ApplyAsync(Event(PublishStatus.Draft, name: "Radni naziv"));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(PublishStatus.Published, name: "Ljetni Festival", changedAt: Noon.AddMinutes(1)));

        (await _fixture.DbContext.ProductSnapshots.AsNoTracking().CountAsync()).Should().Be(1);
        var stored = await StoredAsync();
        stored!.Name.Should().Be("Ljetni Festival");
        stored.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task ApplyAsync_ForTheSameEventTwice_IsIdempotent()
    {
        // At-least-once delivery: the identical message can and will arrive again.
        var message = Event();

        await _sut.ApplyAsync(message);
        _fixture.DbContext.ChangeTracker.Clear();
        await _sut.ApplyAsync(message);

        (await _fixture.DbContext.ProductSnapshots.AsNoTracking().CountAsync()).Should().Be(1);
        (await StoredAsync())!.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task ApplyAsync_ForAnOlderEventThanTheRowItHolds_IgnoresIt()
    {
        // The failure this guard exists for: a redelivered "draft" landing after the real "published"
        // would silently take a live product's sectors off sale, with nothing in the logs to say a
        // sale was refused because of a message from ten minutes ago.
        await _sut.ApplyAsync(Event(PublishStatus.Published, changedAt: Noon));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(PublishStatus.Draft, name: "Stari naziv", changedAt: Noon.AddMinutes(-5)));

        var stored = await StoredAsync();
        stored!.Status.Should().Be(PublishStatus.Published);
        stored.Name.Should().Be("Ljetni Festival");
        stored.ChangedAt.Should().Be(Noon);
    }

    [Fact]
    public async Task ApplyAsync_ForAnEventAtExactlyTheStoredInstant_StillApplies()
    {
        // Equal timestamps are applied, not dropped: two edits inside the same clock tick are far
        // more likely than a genuine duplicate that differs in content, and applying the later
        // arrival is the safer of the two guesses.
        await _sut.ApplyAsync(Event(PublishStatus.Draft, changedAt: Noon));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(PublishStatus.Published, changedAt: Noon));

        (await StoredAsync())!.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task RemoveAsync_HardDeletesTheRow()
    {
        // Hard delete, per .claude/rules/00-workflow-and-testing.md — there is no IsDeleted anywhere
        // in this system, and an "inactive" snapshot row would be a second way to say unavailable.
        await _sut.ApplyAsync(Event());
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.RemoveAsync(_productId);

        (await StoredAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ForAProductItNeverProjected_DoesNothing()
    {
        var act = async () => await _sut.RemoveAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureAsync_ForAnUnknownProduct_StagesTheRowWithoutSaving()
    {
        // The seam SectorService uses: the snapshot has to join the caller's single
        // SaveChangesAsync, not commit on its own.
        await _sut.EnsureAsync(Event());

        (await StoredAsync()).Should().BeNull();

        await _fixture.DbContext.SaveChangesAsync();
        (await StoredAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureAsync_ForAKnownProduct_LeavesTheExistingRowAlone()
    {
        // Deliberately not an upsert. EnsureAsync's data comes from an HTTP read with no idea when
        // Catalog produced it, so letting it win could put a stale status back over a live event.
        await _sut.ApplyAsync(Event(PublishStatus.Published, name: "Ljetni Festival"));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.EnsureAsync(Event(PublishStatus.Draft, name: "Nešto drugo", changedAt: Noon.AddHours(1)));
        await _fixture.DbContext.SaveChangesAsync();

        var stored = await StoredAsync();
        stored!.Status.Should().Be(PublishStatus.Published);
        stored.Name.Should().Be("Ljetni Festival");
    }

    // ── Backfill ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A sector for a product this service holds no snapshot for — exactly the state every
    /// row in the table was in the moment before this read model shipped.</summary>
    private async Task SeedSectorWithoutSnapshotAsync(Guid productId)
    {
        _fixture.DbContext.Sectors.Add(new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OrganizationId = _organizationId,
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private void MockCatalogProducts(params CatalogProductResponse[] products) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid> ids, CancellationToken _) =>
                products.Where(p => ids.Contains(p.Id)).ToList());

    private CatalogProductResponse CatalogProduct(Guid productId, PublishStatus status = PublishStatus.Published) =>
        new(productId, _organizationId, status, TicketingMode.SingleOccurrence, "Ljetni Festival", null, City.Sarajevo);

    [Fact]
    public async Task BackfillAsync_FillsSnapshotsForProductsThisServiceAlreadySellsFor()
    {
        await SeedSectorWithoutSnapshotAsync(_productId);
        MockCatalogProducts(CatalogProduct(_productId));

        var written = await _sut.BackfillAsync();

        written.Should().Be(1);
        (await StoredAsync())!.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task BackfillAsync_WithNothingMissing_AsksCatalogNothing()
    {
        // The steady state, which runs every six hours forever — it must not turn into a periodic
        // full re-read of the catalogue.
        await SeedSectorWithoutSnapshotAsync(_productId);
        MockCatalogProducts(CatalogProduct(_productId));
        await _sut.BackfillAsync();
        _fixture.CatalogClient.Invocations.Clear();

        var written = await _sut.BackfillAsync();

        written.Should().Be(0);
        _fixture.CatalogClient.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task BackfillAsync_ForAProductCatalogNoLongerHas_LeavesItWithoutASnapshot()
    {
        // A product deleted before this service ever projected it. No row means its sectors stay
        // unlistable, which is the correct end state — inventing one would put it back on sale.
        await SeedSectorWithoutSnapshotAsync(_productId);
        MockCatalogProducts();

        var written = await _sut.BackfillAsync();

        written.Should().Be(0);
        (await StoredAsync()).Should().BeNull();
    }

    [Fact]
    public async Task BackfillAsync_IgnoresProductsWithNoSectorsHere()
    {
        // Scoped to what this service actually sells. A catalogue-wide pull would be a much larger
        // query answering a question nobody asked.
        MockCatalogProducts(CatalogProduct(Guid.NewGuid()));

        var written = await _sut.BackfillAsync();

        written.Should().Be(0);
        _fixture.CatalogClient.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task BackfillAsync_CopiesTheDraftStatusThroughRatherThanAssumingPublished()
    {
        // The tempting shortcut — "it has sectors, so it must be live" — would put every draft
        // product on the public listing the moment the backfill ran.
        await SeedSectorWithoutSnapshotAsync(_productId);
        MockCatalogProducts(CatalogProduct(_productId, PublishStatus.Draft));

        await _sut.BackfillAsync();

        (await StoredAsync())!.Status.Should().Be(PublishStatus.Draft);
    }

    public void Dispose() => _fixture.Dispose();
}
