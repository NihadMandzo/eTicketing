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
            .ReturnsAsync(new CatalogProductResponse(_productId, _orgA, PublishStatus.Published, TicketingMode.DailyEntry, "Test proizvod", null, City.Sarajevo));
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
    private static ClaimsPrincipal PlatformStaffCaller() => BuildCaller("SuperAdmin");

    /// <summary>What http.User is on an anonymous request: authenticated is false and there are no
    /// claims at all. GET ticket-types is an AllowAnonymous route, so this is the caller the
    /// publication check has to be right about.</summary>
    private static ClaimsPrincipal AnonymousCaller() => new(new ClaimsIdentity());

    /// <summary>Creates the sector as Draft — SectorService.CreateAsync always does, per the
    /// preview-then-publish pattern — which is exactly the state an organizer adds ticket types in.</summary>
    private async Task<Sector> CreateOwnedSectorAsync()
    {
        var created = await _sectorService.CreateAsync(
            new UpsertSectorRequest { ProductId = _productId, Name = "August 2026", Capacity = 100, Price = 10, PeriodYear = 2026, PeriodMonth = 8 },
            OrgACaller());
        return (await _fixture.SectorRepository.GetByIdAsync(created.Value!.Id))!;
    }

    private async Task<Sector> CreateOwnedPublishedSectorAsync()
    {
        var sector = await CreateOwnedSectorAsync();
        await _sectorService.PublishAsync(sector.Id, OrgACaller());
        return (await _fixture.SectorRepository.GetByIdAsync(sector.Id))!;
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
    public async Task CreateAsync_ForOtherOrganizationsSectorAsPlatformStaff_Succeeds()
    {
        // PlatformStaff (SuperAdmin/Admin) bypasses ownership entirely, per AuthorizeOwnership's
        // own doc comment — this is the one branch of that check with no prior coverage.
        var sector = await CreateOwnedSectorAsync();

        var result = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, BuildCaller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.SectorId.Should().Be(sector.Id);
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

        var result = await _sut.GetBySectorAsync(sector.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(t => t.Name == "Odrasli" && t.Price == 10);
    }

    [Fact]
    public async Task GetBySectorAsync_ForPublishedSector_ReturnsTicketTypesToAnAnonymousCaller()
    {
        var sector = await CreateOwnedPublishedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id, AnonymousCaller());

        // The storefront reads this without a session — a published sector's tiers are public by
        // definition, since they are what the buyer picks between.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(t => t.Name == "Odrasli");
    }

    [Fact]
    public async Task GetBySectorAsync_ForDraftSector_ReturnsNotFoundToAnAnonymousCaller()
    {
        var sector = await CreateOwnedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Tajna cijena", Price = 999 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id, AnonymousCaller());

        // Unannounced names and prices. NotFound rather than Unauthorized on purpose: a 403 would
        // confirm the id names a real sector that simply is not live yet.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
    }

    [Fact]
    public async Task GetBySectorAsync_ForDraftSector_ReturnsNotFoundToAnotherOrganization()
    {
        var sector = await CreateOwnedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Tajna cijena", Price = 999 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
    }

    [Fact]
    public async Task GetBySectorAsync_ForDraftSector_ReturnsTicketTypesToTheOwningOrganizer()
    {
        // The reason publication alone cannot be the gate: the desktop sector dialog lists a
        // sector's tiers while it is still a Draft, which is the whole point of the step.
        var sector = await CreateOwnedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(t => t.Name == "Odrasli");
    }

    [Fact]
    public async Task GetBySectorAsync_ForDraftSector_ReturnsTicketTypesToPlatformStaff()
    {
        var sector = await CreateOwnedSectorAsync();
        await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        var result = await _sut.GetBySectorAsync(sector.Id, PlatformStaffCaller());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetBySectorAsync_ForUnknownSector_ReturnsNotFound()
    {
        var result = await _sut.GetBySectorAsync(Guid.NewGuid(), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sector.not_found");
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
        var remaining = await _sut.GetBySectorAsync(sector.Id, OrgACaller());
        remaining.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenTicketTypeHasTicketsSold_IsRejectedByTheDatabasesForeignKeyRestrict()
    {
        // Regression-proofs TicketConfiguration's Ticket.TicketType relationship
        // (OnDelete(ClientNoAction), which forces the database's own FK restrict instead of EF's
        // client-side fixup — see that config's doc comment) — a TicketType that already has
        // Tickets sold against it must not be deletable out from under them.
        var sector = await CreateOwnedSectorAsync();
        var created = await _sut.CreateAsync(sector.Id, new UpsertTicketTypeRequest { Name = "Odrasli", Price = 10 }, OrgACaller());

        await _fixture.TicketRepository.AddAsync(Ticket.ForSingleOccurrence(
            sector.Id, created.Value!.Id, Guid.NewGuid(), _productId, Guid.NewGuid(), "buyer@example.com", 10));
        await _fixture.UnitOfWork.SaveChangesAsync();

        var act = async () => await _sut.DeleteAsync(sector.Id, created.Value.Id, OrgACaller());

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    public void Dispose() => _fixture.Dispose();
}
