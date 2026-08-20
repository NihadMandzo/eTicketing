using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Sectors;

public class TicketTypeServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketTypeService _sut;
    private readonly ISectorService _sectorService;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public TicketTypeServiceTests()
    {
        _sut = _fixture.CreateTicketTypeService();
        _sectorService = _fixture.CreateSectorService();

        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(_productId, _orgA, PublishStatus.Published, TicketingMode.DailyEntry));
    }

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

    private async Task<Sector> CreateOwnedSectorAsync()
    {
        var created = await _sectorService.CreateAsync(
            new UpsertSectorRequest { ProductId = _productId, Name = "August 2026", Capacity = 100, Price = 10, PeriodYear = 2026, PeriodMonth = 8 },
            OrgACaller());
        return (await _fixture.SectorRepository.GetByIdAsync(created.Value!.Id))!;
    }

    [Fact]
    public async Task CreateAsync_ForOwnedPublishedSector_CreatesTicketType()
    {
        var sector = await CreateOwnedSectorAsync();

        var result = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Odrasli");
        result.Value.SectorId.Should().Be(sector.Id);
    }

    [Fact]
    public async Task CreateAsync_ForOtherOrganizationsSector_ReturnsUnauthorized()
    {
        var sector = await CreateOwnedSectorAsync();

        var result = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket_type.forbidden");
    }

    [Fact]
    public async Task CreateAsync_ForUnknownSector_ReturnsNotFound()
    {
        var result = await _sut.CreateAsync(Guid.NewGuid(), new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
    }

    [Fact]
    public async Task GetBySectorAsync_ReturnsAllTicketTypesForThatSector()
    {
        var sector = await CreateOwnedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Djeca", Price = 4 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(t => t.Name == "Odrasli" && t.Price == 10);
    }

    [Fact]
    public async Task UpdateAsync_ForOwnedSector_UpdatesNameAndPrice()
    {
        var sector = await CreateOwnedSectorAsync();
        var created = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        var result = await _sut.UpdateAsync(sector.Id, created.Value!.Id, new UpsertTicketTypeRequest { Name = "Odrasli (izmijenjeno)", Price = 12 }, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Odrasli (izmijenjeno)");
        result.Value.Price.Should().Be(12);
    }

    [Fact]
    public async Task DeleteAsync_ForOwnedTicketType_RemovesIt()
    {
        var sector = await CreateOwnedSectorAsync();
        var created = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        var result = await _sut.DeleteAsync(sector.Id, created.Value!.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        var remaining = await _sut.GetBySectorAsync(sector.Id);
        remaining.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenTicketTypeHasTicketsSold_IsRejectedByTheDatabasesForeignKeyRestrict()
    {
        // Regression-proofs TicketTypeConfiguration's OnDelete(Restrict) — a TicketType that
        // already has Tickets sold against it must not be deletable out from under them.
        var sector = await CreateOwnedSectorAsync();
        var created = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        await _fixture.TicketRepository.AddAsync(new Ticket
        {
            Id = Guid.NewGuid(),
            SectorId = sector.Id,
            TicketTypeId = created.Value!.Id,
            OrderId = Guid.NewGuid(),
            ProductId = _productId,
            UserId = Guid.NewGuid(),
            UserEmail = "buyer@example.com",
            Status = TicketStatus.Confirmed,
            PricePaid = 10,
        });
        await _fixture.UnitOfWork.SaveChangesAsync();

        var act = async () => await _sut.DeleteAsync(sector.Id, created.Value.Id, OrgACaller());

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    public void Dispose() => _fixture.Dispose();
}
