using System.Security.Claims;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// Catalog's side of the ProductSnapshot read model: publish the product's state on every change.
///
/// <para>Two things make these worth having beyond the existing product.updated tests. First, this
/// event has to fire where that one deliberately does not — on create, on publish, and on draft
/// edits — because a projection that only hears about changes somebody decided were worth emailing
/// about goes stale silently, and the symptom appears in a different service as sectors that will
/// not sell. Second, eTicketing.Ticketing now refuses to list, hold or sell a sector whose snapshot
/// does not say Published, so a missed publish here is an outage there.</para>
/// </summary>
public class ProductSnapshotPublishingTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private Category _music = null!;
    private Category _parking = null!;

    public ProductSnapshotPublishingTests()
    {
        _sut = _fixture.CreateProductService();
        SeedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateAsync_PublishesASnapshotSayingDraft()
    {
        // Created as Draft and said so. A snapshot claiming Published here would put a product on
        // the public storefront before its organizer ever pressed publish.
        var created = await _sut.CreateAsync(Request(), Caller());

        var snapshot = CapturedSnapshot();
        snapshot.ProductId.Should().Be(created.Value!.Id);
        snapshot.OrganizationId.Should().Be(_orgA);
        snapshot.Name.Should().Be("Ljetni Festival");
        snapshot.Status.Should().Be(PublishStatus.Draft);
        snapshot.City.Should().Be(City.Sarajevo);
        snapshot.TicketingMode.Should().Be(TicketingMode.SingleOccurrence);
    }

    [Fact]
    public async Task PublishAsync_PublishesASnapshotSayingPublished()
    {
        // The transition the whole read model exists to carry.
        var created = await _sut.CreateAsync(Request(), Caller());
        _fixture.EventPublisher.Invocations.Clear();

        await _sut.PublishAsync(created.Value!.Id, Caller());

        CapturedSnapshot().Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task UpdateAsync_OnADraft_StillPublishesASnapshot()
    {
        // product.updated deliberately stays silent for drafts — nobody bought a ticket, so there is
        // nobody to email. The snapshot is not a notification and must fire anyway.
        var created = await _sut.CreateAsync(Request(), Caller());
        _fixture.EventPublisher.Invocations.Clear();

        await _sut.UpdateAsync(created.Value!.Id, Request() with { Name = "Novi naziv" }, Caller());

        CapturedSnapshot().Name.Should().Be("Novi naziv");
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductUpdated, It.IsAny<ProductUpdated>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNoVisibleFieldChanged_StillPublishesASnapshot()
    {
        // Same request in, no diff out, no email — and still a snapshot, because "no change worth
        // emailing about" is not the same statement as "no change".
        var created = await _sut.CreateAsync(Request(), Caller());
        await _sut.PublishAsync(created.Value!.Id, Caller());
        _fixture.EventPublisher.Invocations.Clear();

        await _sut.UpdateAsync(created.Value.Id, Request(), Caller());

        CapturedSnapshot().ProductId.Should().Be(created.Value.Id);
    }

    [Fact]
    public async Task UpdateAsync_ThatMovesTheProductToACategoryWithAnotherTicketingMode_ReportsTheNewMode()
    {
        // TicketingMode lives on Category, so a recategorisation changes how every sector of this
        // product behaves. A snapshot still saying SingleOccurrence would leave Ticketing branching
        // on the wrong mode.
        var created = await _sut.CreateAsync(Request(), Caller());
        _fixture.EventPublisher.Invocations.Clear();

        await _sut.UpdateAsync(
            created.Value!.Id, Request() with { CategoryId = _parking.Id, Date = null }, Caller());

        CapturedSnapshot().TicketingMode.Should().Be(TicketingMode.RecurringReservation);
    }

    [Fact]
    public async Task UpdateAsync_KeepsTheEditAndTheSnapshotInOneTransaction()
    {
        // The outbox property, checked against the real publisher rather than a mock: the row that
        // announces the new state has to be committed by the same SaveChangesAsync that wrote it.
        var service = _fixture.CreateProductService(_fixture.OutboxPublisher);
        var created = await service.CreateAsync(Request(), Caller());

        var rows = await _fixture.DbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.RoutingKey == EventNames.ProductSnapshotChanged).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Payload.Should().Contain(created.Value!.Id.ToString());
    }

    [Fact]
    public async Task DeleteAsync_PublishesProductDeletedRatherThanASnapshot()
    {
        // The delete side is the existing product.deleted event, which Ticketing already consumes —
        // it hard-deletes the snapshot row. A "snapshot" of a product that no longer exists would be
        // a second way of saying the same thing, and the two could disagree.
        var created = await _sut.CreateAsync(Request(), Caller());
        _fixture.EventPublisher.Invocations.Clear();

        await _sut.DeleteAsync(created.Value!.Id, Caller());

        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductSnapshotChanged, It.IsAny<ProductSnapshotChanged>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductDeleted, It.IsAny<ProductDeleted>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PreviewAsync_PublishesNothing()
    {
        // Preview is stateless by contract (R34) and has no product to describe yet.
        await _sut.PreviewAsync(Request(), Caller());

        _fixture.EventPublisher.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ForADateLessProduct_CarriesANullDateRatherThanADefault()
    {
        // DailyEntry and RecurringReservation products have no single date. DateTime.MinValue on the
        // wire would read as a real date in 0001 to anything that renders it.
        await _sut.CreateAsync(Request() with { CategoryId = _parking.Id, Date = null }, Caller());

        CapturedSnapshot().Date.Should().BeNull();
    }

    /// <summary>The single <see cref="ProductSnapshotChanged"/> the call under test published.
    /// Fails loudly rather than returning null when there is none — "no event" is the failure these
    /// tests exist to catch.</summary>
    private ProductSnapshotChanged CapturedSnapshot()
    {
        var published = _fixture.EventPublisher.Invocations
            .Where(i => (string)i.Arguments[0] == EventNames.ProductSnapshotChanged)
            .Select(i => (ProductSnapshotChanged)i.Arguments[1])
            .ToList();

        published.Should().ContainSingle("the change under test must publish exactly one snapshot");
        return published[0];
    }

    /// <summary>Relative to now for the same reason ProductUpdateNotificationTests does it —
    /// ProductService.ValidateAsync rejects a past date against the real clock.</summary>
    private static readonly DateTime BaseDate =
        new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 20, 0, 0, DateTimeKind.Utc)
            .AddDays(30);

    private UpsertProductRequest Request() => new()
    {
        Name = "Ljetni Festival",
        Description = "Opis festivala",
        Date = BaseDate,
        CategoryId = _music.Id,
        Latitude = 43.8563,
        Longitude = 18.4131,
        City = City.Sarajevo,
    };

    private async Task SeedAsync()
    {
        _music = new Category { Name = "Pozorište", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _parking = new Category { Name = "Parking", IsActive = true, TicketingMode = TicketingMode.RecurringReservation };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.CategoryRepository.AddAsync(_parking);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    private ClaimsPrincipal Caller()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "OrganizationSuperAdmin"),
            new("organizationId", _orgA.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
