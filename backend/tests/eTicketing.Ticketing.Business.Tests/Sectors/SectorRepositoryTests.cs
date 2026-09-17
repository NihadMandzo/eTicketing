using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Business.Tests.Sectors;

/// <summary>
/// The two read methods TicketPrintService used to compose inline off ISectorRepository.Query().
/// Their tracking behaviour is the part worth pinning: the two are deliberately different, and
/// getting the second one wrong is silent until EF tries to INSERT sectors that already exist.
/// </summary>
public class SectorRepositoryTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    private async Task<Sector> SeedSectorAsync(
        string name, Guid productId, PublishStatus status, params string[] ticketTypeNames)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OrganizationId = _organizationId,
            Name = name,
            Capacity = 100,
            Price = 10,
            Status = status,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        await _fixture.SectorRepository.AddAsync(sector);

        foreach (var ticketTypeName in ticketTypeNames)
        {
            await _fixture.TicketTypeRepository.AddAsync(new TicketType
            {
                Id = Guid.NewGuid(),
                SectorId = sector.Id,
                Name = ticketTypeName,
                Price = 5,
            });
        }

        await _fixture.UnitOfWork.SaveChangesAsync();
        return sector;
    }

    /// <summary>Entities seeded above are still tracked from the insert, which would make any
    /// tracking assertion below meaningless. Clearing puts the context in the state a real
    /// request-scoped one starts in.</summary>
    private void DetachEverything() => _fixture.DbContext.ChangeTracker.Clear();

    // --- GetPublishedByProductWithTicketTypesAsync ---

    [Fact]
    public async Task GetPublishedByProductWithTicketTypesAsync_ReturnsOnlyThatProductsPublishedSectors()
    {
        await SeedSectorAsync("Parter", _productId, PublishStatus.Published);
        await SeedSectorAsync("Draft sektor", _productId, PublishStatus.Draft);
        await SeedSectorAsync("Tuđi proizvod", _otherProductId, PublishStatus.Published);
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetPublishedByProductWithTicketTypesAsync(_productId);

        sectors.Should().ContainSingle().Which.Name.Should().Be("Parter");
    }

    [Fact]
    public async Task GetPublishedByProductWithTicketTypesAsync_OrdersByName()
    {
        // The print-options screen lists sectors for a human to pick from, so the order is part of
        // the contract — it was an OrderBy(s => s.Name) on the inline query this replaced.
        await SeedSectorAsync("Zapad", _productId, PublishStatus.Published);
        await SeedSectorAsync("Istok", _productId, PublishStatus.Published);
        await SeedSectorAsync("Parter", _productId, PublishStatus.Published);
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetPublishedByProductWithTicketTypesAsync(_productId);

        sectors.Select(s => s.Name).Should().ContainInOrder("Istok", "Parter", "Zapad");
    }

    [Fact]
    public async Task GetPublishedByProductWithTicketTypesAsync_LoadsTicketTypes()
    {
        await SeedSectorAsync("Parter", _productId, PublishStatus.Published, "Odrasli", "Djeca");
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetPublishedByProductWithTicketTypesAsync(_productId);

        sectors.Single().TicketTypes.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPublishedByProductWithTicketTypesAsync_DoesNotTrackWhatItReturns()
    {
        await SeedSectorAsync("Parter", _productId, PublishStatus.Published, "Odrasli");
        DetachEverything();

        await _fixture.SectorRepository.GetPublishedByProductWithTicketTypesAsync(_productId);

        _fixture.DbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetPublishedByProductWithTicketTypesAsync_ForAProductWithNoPublishedSectors_ReturnsEmpty()
    {
        await SeedSectorAsync("Draft sektor", _productId, PublishStatus.Draft);
        DetachEverything();

        (await _fixture.SectorRepository.GetPublishedByProductWithTicketTypesAsync(_productId))
            .Should().BeEmpty();
    }

    // --- GetByIdsWithTicketTypesAsync ---

    [Fact]
    public async Task GetByIdsWithTicketTypesAsync_TracksWhatItReturns()
    {
        // The reason this one is not AsNoTracking: TicketPrintService hands these Sector instances
        // to Ticket.ForPrint and saves the tickets in the same SaveChangesAsync. Untracked, EF
        // would see each Sector as a new row and try to insert it.
        var sector = await SeedSectorAsync("Parter", _productId, PublishStatus.Published);
        DetachEverything();

        await _fixture.SectorRepository.GetByIdsWithTicketTypesAsync([sector.Id]);

        _fixture.DbContext.ChangeTracker.Entries<Sector>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task GetByIdsWithTicketTypesAsync_ReturnsEveryRequestedSectorInOneQuery()
    {
        var first = await SeedSectorAsync("Parter", _productId, PublishStatus.Published, "Odrasli");
        var second = await SeedSectorAsync("Balkon", _productId, PublishStatus.Published);
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetByIdsWithTicketTypesAsync([first.Id, second.Id]);

        sectors.Should().HaveCount(2);
        sectors.Single(s => s.Id == first.Id).TicketTypes.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIdsWithTicketTypesAsync_IgnoresUnknownIds()
    {
        // A print batch naming a sector that was deleted between the options screen and submit.
        // The caller (ResolveLines) turns the gap into a validation error; the repository just
        // reports what exists.
        var sector = await SeedSectorAsync("Parter", _productId, PublishStatus.Published);
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetByIdsWithTicketTypesAsync([sector.Id, Guid.NewGuid()]);

        sectors.Should().ContainSingle().Which.Id.Should().Be(sector.Id);
    }

    [Fact]
    public async Task GetByIdsWithTicketTypesAsync_ForAnEmptyIdList_ReturnsEmptyWithoutQuerying()
    {
        await SeedSectorAsync("Parter", _productId, PublishStatus.Published);
        DetachEverything();

        var sectors = await _fixture.SectorRepository.GetByIdsWithTicketTypesAsync([]);

        // Short-circuited rather than sent as `WHERE Id IN ()`, which some providers reject and
        // all of them scan for nothing. Same guard as GetPublishedCapacityByProductAsync.
        sectors.Should().BeEmpty();
        _fixture.DbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsWithTicketTypesAsync_ReturnsDraftSectorsToo()
    {
        // Unlike the published-by-product read: a print batch is an organizer action on their own
        // product, and the publication check belongs to the caller, not this lookup.
        var draft = await SeedSectorAsync("Draft sektor", _productId, PublishStatus.Draft);
        DetachEverything();

        (await _fixture.SectorRepository.GetByIdsWithTicketTypesAsync([draft.Id]))
            .Should().ContainSingle();
    }

    public void Dispose() => _fixture.Dispose();
}
