using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.GateDevices;

/// <summary>
/// Registration and lifecycle of gate scanners. The load-bearing case in here is
/// <see cref="CreateAsync_WithASectorFromAnotherProduct_IsRejected"/>: without that rule, a device
/// registered against tonight's concert could be scoped to next week's match's sector, and the
/// whole sector check would be defeated one layer above where it is enforced.
/// </summary>
public class GateDeviceServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IGateDeviceService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();
    private readonly Guid _organizerUserId = Guid.NewGuid();

    public GateDeviceServiceTests()
    {
        _sut = _fixture.CreateGateDeviceService();
        MockProduct(_productId, _orgA);
        MockProduct(_otherProductId, _orgA);
    }

    // ------------------------------------------------------------------------------------ create

    [Fact]
    public async Task CreateAsync_WithSeveralSectorsOfTheProduct_StoresTheScopeAndReturnsTheKeyOnce()
    {
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");

        var result = await _sut.CreateAsync(Request(sectorIds: [vip.Id, loza.Id]), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().StartWith(GateDeviceKeyGenerator.Prefix);
        result.Value.Device.Sectors.Should().HaveCount(2);
        result.Value.Device.KeyPrefix.Should().Be(result.Value.ApiKey[..GateDeviceKeyGenerator.DisplayPrefixLength]);
        result.Value.Device.AllSectors.Should().BeFalse();

        var persisted = await _fixture.DbContext.GateDevices
            .Include(d => d.Sectors)
            .SingleAsync(d => d.Id == result.Value.Device.Id);

        persisted.OrganizationId.Should().Be(_orgA);
        persisted.CreatedByUserId.Should().Be(_organizerUserId);
        persisted.Sectors.Select(s => s.SectorId).Should().BeEquivalentTo([vip.Id, loza.Id]);
    }

    [Fact]
    public async Task CreateAsync_NeverPersistsThePlaintextKey()
    {
        var vip = await SeedSectorAsync("VIP");

        var result = await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));
        var apiKey = result.Value!.ApiKey;

        var persisted = await _fixture.DbContext.GateDevices.SingleAsync();
        persisted.KeyHash.Should().Be(GateDeviceKeyGenerator.Hash(apiKey));
        persisted.KeyHash.Should().NotBe(apiKey);
        // The stored prefix is a display aid, not a fragment usable to reconstruct the secret.
        persisted.KeyPrefix.Length.Should().BeLessThan(apiKey.Length);
    }

    [Fact]
    public async Task CreateAsync_MintsADistinctKeyEveryTime()
    {
        var vip = await SeedSectorAsync("VIP");

        var first = await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));
        var second = await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));

        first.Value!.ApiKey.Should().NotBe(second.Value!.ApiKey);
    }

    [Fact]
    public async Task CreateAsync_WithASectorFromAnotherProduct_IsRejected()
    {
        var foreignSector = await SeedSectorAsync("VIP", productId: _otherProductId);

        var result = await _sut.CreateAsync(Request(sectorIds: [foreignSector.Id]), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.sector_not_in_product");
        (await _fixture.DbContext.GateDevices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WithOneValidAndOneForeignSector_RejectsTheWholeRequest()
    {
        var vip = await SeedSectorAsync("VIP");
        var foreignSector = await SeedSectorAsync("VIP", productId: _otherProductId);

        var result = await _sut.CreateAsync(Request(sectorIds: [vip.Id, foreignSector.Id]), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.sector_not_in_product");
    }

    [Fact]
    public async Task CreateAsync_WithAnUnknownSectorId_IsRejected()
    {
        var result = await _sut.CreateAsync(Request(sectorIds: [Guid.NewGuid()]), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.sector_not_in_product");
    }

    [Fact]
    public async Task CreateAsync_WithNeitherSectorsNorAllSectors_IsRejected()
    {
        // Belt-and-braces behind the validator: an empty scope must never silently mean "all".
        var result = await _sut.CreateAsync(Request(sectorIds: []), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.no_sectors");
    }

    [Fact]
    public async Task CreateAsync_WithAllSectors_StoresNoSectorRows()
    {
        var result = await _sut.CreateAsync(
            Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Device.AllSectors.Should().BeTrue();
        (await _fixture.DbContext.GateDeviceSectors.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_ForAnotherOrganizationsProduct_IsForbidden()
    {
        var result = await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), OrganizerOf(_orgB));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.forbidden");
    }

    [Fact]
    public async Task CreateAsync_AsPlatformStaffForAnyOrganization_IsAllowedAndInheritsTheProductsOrganization()
    {
        var result = await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), PlatformStaff());

        result.IsSuccess.Should().BeTrue();
        (await _fixture.DbContext.GateDevices.SingleAsync()).OrganizationId.Should().Be(_orgA);
    }

    [Fact]
    public async Task CreateAsync_ForAProductCatalogDoesNotKnow_IsRejected()
    {
        var unknownProductId = Guid.NewGuid();
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(unknownProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogProductResponse?)null);

        var result = await _sut.CreateAsync(
            Request(allSectors: true, sectorIds: [], productId: unknownProductId), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.product_not_found");
    }

    [Fact]
    public async Task CreateAsync_WithADuplicatedSectorId_StoresItOnce()
    {
        // The unique (GateDeviceId, SectorId) index would otherwise reject the whole write.
        var vip = await SeedSectorAsync("VIP");

        var result = await _sut.CreateAsync(Request(sectorIds: [vip.Id, vip.Id]), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        (await _fixture.DbContext.GateDeviceSectors.CountAsync()).Should().Be(1);
    }

    // ------------------------------------------------------------------------------------ update

    [Fact]
    public async Task UpdateAsync_AddingASector_WidensTheScopeWithoutTouchingTheKey()
    {
        // This is what makes a gate re-scopable without a re-flash: the device keeps its key and
        // picks the new sector up on its next config refresh.
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.UpdateAsync(
            created.Device.Id, Request(sectorIds: [vip.Id, loza.Id]), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Sectors.Should().HaveCount(2);
        result.Value.KeyPrefix.Should().Be(created.Device.KeyPrefix);

        var persisted = await _fixture.DbContext.GateDevices.Include(d => d.Sectors)
            .SingleAsync(d => d.Id == created.Device.Id);
        persisted.KeyHash.Should().Be(GateDeviceKeyGenerator.Hash(created.ApiKey));
        persisted.Sectors.Select(s => s.SectorId).Should().BeEquivalentTo([vip.Id, loza.Id]);
    }

    [Fact]
    public async Task UpdateAsync_RemovingASector_NarrowsTheScope()
    {
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id, loza.Id]), OrganizerOf(_orgA))).Value!;

        await _sut.UpdateAsync(created.Device.Id, Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));

        var persisted = await _fixture.DbContext.GateDevices.Include(d => d.Sectors)
            .SingleAsync(d => d.Id == created.Device.Id);
        persisted.Sectors.Select(s => s.SectorId).Should().BeEquivalentTo([vip.Id]);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingToAllSectors_DropsTheExplicitSectorRows()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.UpdateAsync(
            created.Device.Id, Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        (await _fixture.DbContext.GateDeviceSectors.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ReSubmittingTheSameSectors_KeepsTheExistingRows()
    {
        // The diff-rather-than-clear-and-readd path: an unchanged sector keeps its row id, and the
        // unique index is never asked to accept a delete and an insert of the same pair at once.
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;
        var originalRowId = (await _fixture.DbContext.GateDeviceSectors.SingleAsync()).Id;

        await _sut.UpdateAsync(created.Device.Id, Request(sectorIds: [vip.Id], name: "Ulaz B"), OrganizerOf(_orgA));

        var rows = await _fixture.DbContext.GateDeviceSectors.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Id.Should().Be(originalRowId);
    }

    [Fact]
    public async Task UpdateAsync_MovingTheDeviceToAnotherProduct_IsRejected()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.UpdateAsync(
            created.Device.Id, Request(allSectors: true, sectorIds: [], productId: _otherProductId), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.product_id_immutable");
    }

    [Fact]
    public async Task UpdateAsync_ForAnotherOrganizationsDevice_IsForbidden()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.UpdateAsync(created.Device.Id, Request(sectorIds: [vip.Id]), OrganizerOf(_orgB));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.forbidden");
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingADevice_PersistsTheFlag()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        await _sut.UpdateAsync(
            created.Device.Id, Request(sectorIds: [vip.Id], isActive: false), OrganizerOf(_orgA));

        (await _fixture.DbContext.GateDevices.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ForAnUnknownDevice_ReturnsNotFound()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.not_found");
    }

    // ------------------------------------------------------------------------------ rotate & delete

    [Fact]
    public async Task RotateKeyAsync_MintsANewKeyAndInvalidatesTheOld()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var rotated = await _sut.RotateKeyAsync(created.Device.Id, OrganizerOf(_orgA));

        rotated.IsSuccess.Should().BeTrue();
        rotated.Value!.ApiKey.Should().NotBe(created.ApiKey);

        // The old key must no longer authenticate — the lookup the handler performs finds nothing.
        (await _fixture.GateDeviceRepository.GetByKeyHashAsync(GateDeviceKeyGenerator.Hash(created.ApiKey)))
            .Should().BeNull();
        (await _fixture.GateDeviceRepository.GetByKeyHashAsync(GateDeviceKeyGenerator.Hash(rotated.Value.ApiKey)))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task RotateKeyAsync_ForAnotherOrganizationsDevice_IsForbidden()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.RotateKeyAsync(created.Device.Id, OrganizerOf(_orgB));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.forbidden");
    }

    [Fact]
    public async Task DeleteAsync_HardDeletesTheDeviceAndCascadesItsSectorRows()
    {
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id, loza.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.DeleteAsync(created.Device.Id, OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        (await _fixture.DbContext.GateDevices.CountAsync()).Should().Be(0);
        (await _fixture.DbContext.GateDeviceSectors.CountAsync()).Should().Be(0);
        // Hard delete, per the platform's no-soft-delete rule — the sectors themselves survive.
        (await _fixture.DbContext.Sectors.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_ForAnotherOrganizationsDevice_IsForbidden()
    {
        var vip = await SeedSectorAsync("VIP");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA))).Value!;

        var result = await _sut.DeleteAsync(created.Device.Id, OrganizerOf(_orgB));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.forbidden");
        (await _fixture.DbContext.GateDevices.CountAsync()).Should().Be(1);
    }

    // ------------------------------------------------------------------------------- search & config

    [Fact]
    public async Task SearchAsync_ForAnOrganizer_ReturnsOnlyTheirOwnOrganizationsDevices()
    {
        var vip = await SeedSectorAsync("VIP");
        await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));
        await SeedForeignDeviceAsync();

        var result = await _sut.SearchAsync(new GateDeviceQuery(), OrganizerOf(_orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.Sectors.Should().ContainSingle().Which.Name.Should().Be("VIP");
    }

    [Fact]
    public async Task SearchAsync_AsPlatformStaff_ReturnsEveryOrganizationsDevices()
    {
        var vip = await SeedSectorAsync("VIP");
        await _sut.CreateAsync(Request(sectorIds: [vip.Id]), OrganizerOf(_orgA));
        await SeedForeignDeviceAsync();

        var result = await _sut.SearchAsync(new GateDeviceQuery(), PlatformStaff());

        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_ForAnOrganizerWithNoOrganizationClaim_IsRejected()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _organizerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
        };

        var result = await _sut.SearchAsync(
            new GateDeviceQuery(), new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.no_organization");
    }

    [Fact]
    public async Task GetConfigAsync_ForAScopedDevice_NamesTheProductAndEverySector()
    {
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");
        var created = (await _sut.CreateAsync(Request(sectorIds: [vip.Id, loza.Id]), OrganizerOf(_orgA))).Value!;
        var device = (await _fixture.GateDeviceRepository.GetByIdWithSectorsAsync(created.Device.Id))!;
        SeedSnapshot(_productId);

        var result = await _sut.GetConfigAsync(device);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductName.Should().Be("Test proizvod");
        result.Value.AllSectors.Should().BeFalse();
        result.Value.Sectors.Select(s => s.Name).Should().BeEquivalentTo(["VIP", "Loža"]);
        result.Value.ServerTime.Should().Be(_fixture.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task GetConfigAsync_ForAnAllSectorsDevice_ReturnsAnEmptySectorList()
    {
        var created = (await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA))).Value!;
        var device = (await _fixture.GateDeviceRepository.GetByIdWithSectorsAsync(created.Device.Id))!;
        SeedSnapshot(_productId);

        var result = await _sut.GetConfigAsync(device);

        result.Value!.AllSectors.Should().BeTrue();
        result.Value.Sectors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfigAsync_ReadsTheProductFromTheSnapshot_WithoutCallingCatalog()
    {
        // The device's own path must keep working while Catalog is down, so the name and date come
        // from the local read model. Registration (above) is what still asks Catalog.
        var created = (await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA))).Value!;
        var device = (await _fixture.GateDeviceRepository.GetByIdWithSectorsAsync(created.Device.Id))!;
        var showing = new DateTime(2026, 8, 24, 19, 30, 0);
        _fixture.UpsertProductSnapshot(_productId, _orgA, TicketingMode.SingleOccurrence, showing, name: "Koncert u Zetri");
        _fixture.CatalogClient.Invocations.Clear();

        var result = await _sut.GetConfigAsync(device);

        result.Value!.ProductName.Should().Be("Koncert u Zetri");
        result.Value.ProductDate.Should().Be(showing);
        _fixture.CatalogClient.Verify(
            c => c.GetProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetConfigAsync_WhenTheProductHasNoSnapshotRow_FailsWithProductNotFound()
    {
        // The product was deleted after the device was registered, which removes its snapshot row.
        var created = (await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA))).Value!;
        var device = (await _fixture.GateDeviceRepository.GetByIdWithSectorsAsync(created.Device.Id))!;

        var result = await _sut.GetConfigAsync(device);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gate_device.product_not_found");
    }

    [Fact]
    public async Task TouchAsync_StampsLastSeenAt()
    {
        var created = (await _sut.CreateAsync(Request(allSectors: true, sectorIds: []), OrganizerOf(_orgA))).Value!;
        created.Device.LastSeenAt.Should().BeNull();

        await _sut.TouchAsync(created.Device.Id);

        (await _fixture.DbContext.GateDevices.SingleAsync()).LastSeenAt
            .Should().Be(_fixture.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task TouchAsync_ForAnUnknownDevice_DoesNotThrow()
    {
        // A bookkeeping column must never be able to turn someone away at a door.
        var act = async () => await _sut.TouchAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ------------------------------------------------------------------------------- test helpers

    private void MockProduct(Guid productId, Guid organizationId)
    {
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                productId, organizationId, PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Test proizvod", new DateTime(2026, 8, 24, 20, 0, 0, DateTimeKind.Utc), City.Sarajevo));
    }

    /// <summary>The device's config path reads the local read model, not Catalog, so the config
    /// tests need a row there as well as the Catalog mock registration uses.</summary>
    private void SeedSnapshot(Guid productId) =>
        _fixture.UpsertProductSnapshot(
            productId, _orgA, TicketingMode.SingleOccurrence, new DateTime(2026, 8, 24, 20, 0, 0));

    private UpsertGateDeviceRequest Request(
        List<Guid> sectorIds,
        bool allSectors = false,
        Guid? productId = null,
        string name = "Ulaz A",
        bool isActive = true) =>
        new()
        {
            ProductId = productId ?? _productId,
            Name = name,
            AllSectors = allSectors,
            SectorIds = sectorIds,
            IsActive = isActive,
        };

    private async Task<Sector> SeedSectorAsync(string name, Guid? productId = null)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? _productId,
            OrganizationId = _orgA,
            Name = name,
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };

        _fixture.DbContext.Sectors.Add(sector);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        return sector;
    }

    private async Task SeedForeignDeviceAsync()
    {
        var key = _fixture.KeyGenerator.Create();
        _fixture.DbContext.GateDevices.Add(new GateDevice
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgB,
            ProductId = Guid.NewGuid(),
            Name = "Tuđi ulaz",
            KeyHash = key.KeyHash,
            KeyPrefix = key.KeyPrefix,
            AllSectors = true,
            IsActive = true,
            CreatedByUserId = Guid.NewGuid(),
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private ClaimsPrincipal OrganizerOf(Guid organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _organizerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
            new(ClaimTypes.Email, "org@example.com"),
            new("organizationId", organizationId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private ClaimsPrincipal PlatformStaff()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _organizerUserId.ToString()),
            new(ClaimTypes.Role, "SuperAdmin"),
            new(ClaimTypes.Email, "staff@example.com"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
