using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

/// <summary>
/// Sector-level scoping — "this door only takes VIP" — for both callers that can ask: an organizer
/// narrowing the request, and a registered gate device whose scope comes from its own row.
///
/// The product-level cases live in TicketValidationServiceTests; everything here is about the
/// finer check layered underneath it, and about the two paths agreeing.
/// </summary>
public class TicketValidationSectorScopeTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketValidationService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _otherProductId = Guid.NewGuid();
    private readonly Guid _gateOwnerUserId = Guid.NewGuid();
    private readonly Guid _scannerUserId = Guid.NewGuid();

    /// <summary>Matches TicketingTestContext.Clock's pinned start.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public TicketValidationSectorScopeTests()
    {
        _sut = _fixture.CreateTicketValidationService();
        MockProduct(_productId);
        MockProduct(_otherProductId);
    }

    // ------------------------------------------------------- device scope: the multi-sector gate

    [Fact]
    public async Task ValidateForDeviceAsync_ForATicketInTheDevicesOnlySector_ReturnsValidAndBurnsIt()
    {
        var vip = await SeedSectorAsync("VIP");
        var ticket = await SeedTicketAsync(vip);
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);

        var result = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(ticket.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.Code.Should().Be("ticket.valid");
        result.Value.SectorName.Should().Be("VIP");

        var persisted = await ReloadAsync(ticket.Id);
        persisted.Status.Should().Be(TicketStatus.Used);
        persisted.ValidatedByDeviceId.Should().Be(device.Id);
        // The organizer who registered the gate stays the accountable party, not the device.
        persisted.ValidatedByUserId.Should().Be(_gateOwnerUserId);
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForATicketInAnotherSectorOfTheSameProduct_ReturnsWrongSector()
    {
        // The requirement: a perfectly good ticket for the same event still must not open the wrong
        // door. Parter is sold, paid for and live — and it is not what this gate admits.
        var vip = await SeedSectorAsync("VIP");
        var parter = await SeedSectorAsync("Parter");
        var ticket = await SeedTicketAsync(parter);
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);

        var result = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(ticket.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_sector");
        result.Value.Message.Should().Be("Ulaznica ne pripada sektoru na ovom ulazu.");

        // Scanning at the wrong door must not burn it — the holder walks to the right gate and
        // gets in with the same ticket.
        (await ReloadAsync(ticket.Id)).Status.Should().Be(TicketStatus.Confirmed);
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForATicketInAnyOfSeveralScopedSectors_ReturnsValid()
    {
        // "The validation can be used for more sectors at a time" — one door, several sectors.
        var vip = await SeedSectorAsync("VIP");
        var loza = await SeedSectorAsync("Loža");
        var parter = await SeedSectorAsync("Parter");
        var device = await SeedDeviceAsync(sectorIds: [vip.Id, loza.Id]);

        var vipTicket = await SeedTicketAsync(vip);
        var lozaTicket = await SeedTicketAsync(loza);
        var parterTicket = await SeedTicketAsync(parter);

        var first = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(vipTicket.Id));
        var second = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(lozaTicket.Id));
        var third = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(parterTicket.Id));

        first.Value!.IsValid.Should().BeTrue();
        second.Value!.IsValid.Should().BeTrue();
        third.Value!.IsValid.Should().BeFalse();
        third.Value.Code.Should().Be("ticket.wrong_sector");
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForAnAllSectorsDevice_AdmitsEverySectorOfTheProduct()
    {
        // The main-entrance case. AllSectors is an explicit opt-in, never an accidentally empty list.
        var vip = await SeedSectorAsync("VIP");
        var parter = await SeedSectorAsync("Parter");
        var device = await SeedDeviceAsync(allSectors: true);

        var vipResult = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign((await SeedTicketAsync(vip)).Id));
        var parterResult = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign((await SeedTicketAsync(parter)).Id));

        vipResult.Value!.IsValid.Should().BeTrue();
        parterResult.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForATicketOnAnotherProduct_ReportsWrongProductNotWrongSector()
    {
        // Ordering matters at a real door: telling someone their ticket is "for another sector"
        // when it is actually for a different event entirely sends them to the wrong place to sort
        // it out. The coarser, more useful reason wins.
        var vip = await SeedSectorAsync("VIP");
        var otherSector = await SeedSectorAsync("VIP", productId: _otherProductId);
        var ticket = await SeedTicketAsync(otherSector);
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);

        var result = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(ticket.Id));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_product");
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForAnotherOrganizationsTicket_IsRejected()
    {
        // A device has no PlatformStaff equivalent — the tenancy check always runs for it.
        var foreignSector = await SeedSectorAsync("VIP", organizationId: _orgB);
        var ticket = await SeedTicketAsync(foreignSector);
        var device = await SeedDeviceAsync(sectorIds: [foreignSector.Id]);

        var result = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(ticket.Id));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_organization");
    }

    [Fact]
    public async Task ValidateForDeviceAsync_ForTheSameTicketTwice_RejectsTheSecondScan()
    {
        var vip = await SeedSectorAsync("VIP");
        var ticket = await SeedTicketAsync(vip);
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);
        var payload = _fixture.QrCodec.Sign(ticket.Id);

        await _sut.ValidateForDeviceAsync(device, payload);
        var second = await _sut.ValidateForDeviceAsync(device, payload);

        second.Value!.IsValid.Should().BeFalse();
        second.Value.Code.Should().Be("ticket.already_used");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("ETK1.00000000000000000000000000000000.forgedsignature")]
    public async Task ValidateForDeviceAsync_ForAnUnparseableCode_ReturnsQrInvalid(string code)
    {
        var vip = await SeedSectorAsync("VIP");
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);

        var result = await _sut.ValidateForDeviceAsync(device, code);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.qr_invalid");
    }

    [Fact]
    public async Task ValidateForDeviceAsync_WhenAnotherScannerHoldsTheLock_Returns409WithoutBurning()
    {
        var vip = await SeedSectorAsync("VIP");
        var ticket = await SeedTicketAsync(vip);
        var device = await SeedDeviceAsync(sectorIds: [vip.Id]);

        _fixture.ValidationLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ValidateForDeviceAsync(device, _fixture.QrCodec.Sign(ticket.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.validation_in_progress");
        (await ReloadAsync(ticket.Id)).Status.Should().Be(TicketStatus.Confirmed);
    }

    // ------------------------------------------------- claims scope: organizer narrowing by hand

    [Fact]
    public async Task ValidateAsync_WithNoSectorIds_AdmitsAnySectorOfTheProduct()
    {
        // Backwards compatibility: the mobile scanner opens on a product, not a door, and sends no
        // SectorIds at all. That must keep behaving exactly as it did before sector scoping existed.
        var parter = await SeedSectorAsync("Parter");
        var ticket = await SeedTicketAsync(parter);

        var result = await _sut.ValidateAsync(
            new ValidateTicketRequest { ProductId = _productId, Code = _fixture.QrCodec.Sign(ticket.Id) },
            OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithAnEmptySectorIdsList_AdmitsAnySectorOfTheProduct()
    {
        var parter = await SeedSectorAsync("Parter");
        var ticket = await SeedTicketAsync(parter);

        var result = await _sut.ValidateAsync(
            new ValidateTicketRequest { ProductId = _productId, Code = _fixture.QrCodec.Sign(ticket.Id), SectorIds = [] },
            OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithSectorIdsNotContainingTheTicketsSector_ReturnsWrongSector()
    {
        var vip = await SeedSectorAsync("VIP");
        var parter = await SeedSectorAsync("Parter");
        var ticket = await SeedTicketAsync(parter);

        var result = await _sut.ValidateAsync(
            new ValidateTicketRequest { ProductId = _productId, Code = _fixture.QrCodec.Sign(ticket.Id), SectorIds = [vip.Id] },
            OrganizerOf(_orgA));

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_sector");
        (await ReloadAsync(ticket.Id)).Status.Should().Be(TicketStatus.Confirmed);
    }

    [Fact]
    public async Task ValidateAsync_AsPlatformStaffWithSectorIds_StillEnforcesTheSectorScope()
    {
        // PlatformStaff overrides ownership, not physics: they are standing at the VIP door too.
        var vip = await SeedSectorAsync("VIP");
        var parter = await SeedSectorAsync("Parter");
        var ticket = await SeedTicketAsync(parter);

        var result = await _sut.ValidateAsync(
            new ValidateTicketRequest { ProductId = _productId, Code = _fixture.QrCodec.Sign(ticket.Id), SectorIds = [vip.Id] },
            PlatformStaff());

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_sector");
    }

    [Fact]
    public async Task ValidateAsync_ForAnOrganizerWithNoOrganizationClaim_IsRejectedRatherThanGrantedEverything()
    {
        // The reason BypassOrganizationCheck is its own flag instead of "a null OrganizationId means
        // skip the check": collapsing the two would turn a malformed token into a platform-wide
        // override.
        var vip = await SeedSectorAsync("VIP");
        var ticket = await SeedTicketAsync(vip);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
        };
        var orphanOrganizer = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var result = await _sut.ValidateAsync(
            new ValidateTicketRequest { ProductId = _productId, Code = _fixture.QrCodec.Sign(ticket.Id) },
            orphanOrganizer);

        result.Value!.IsValid.Should().BeFalse();
        result.Value.Code.Should().Be("ticket.wrong_organization");
        (await ReloadAsync(ticket.Id)).Status.Should().Be(TicketStatus.Confirmed);
    }

    // ------------------------------------------------------------------------------- test helpers

    private void MockProduct(Guid productId)
    {
        var response = new CatalogProductResponse(
            productId, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
            "Test proizvod", Today.ToDateTime(new TimeOnly(20, 0)), City.Sarajevo);

        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private async Task<Sector> SeedSectorAsync(string name, Guid? productId = null, Guid? organizationId = null)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? _productId,
            OrganizationId = organizationId ?? _orgA,
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

    private async Task<Ticket> SeedTicketAsync(Sector sector)
    {
        var ticket = Ticket.ForSingleOccurrence(
            sector.Id, null, Guid.NewGuid(), sector.ProductId, Guid.NewGuid(), "buyer@example.com", 50);
        ticket.Status = TicketStatus.Confirmed;

        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();

        // Detached so the service genuinely re-reads from the database rather than getting handed
        // back the instance this method already tracked.
        _fixture.DbContext.ChangeTracker.Clear();
        return ticket;
    }

    private async Task<GateDevice> SeedDeviceAsync(
        List<Guid>? sectorIds = null, bool allSectors = false, Guid? productId = null, Guid? organizationId = null)
    {
        var key = _fixture.KeyGenerator.Create();
        var device = new GateDevice
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId ?? _orgA,
            ProductId = productId ?? _productId,
            Name = "Ulaz A",
            KeyHash = key.KeyHash,
            KeyPrefix = key.KeyPrefix,
            AllSectors = allSectors,
            IsActive = true,
            CreatedByUserId = _gateOwnerUserId,
        };

        foreach (var sectorId in sectorIds ?? [])
        {
            device.Sectors.Add(new GateDeviceSector { Id = Guid.NewGuid(), SectorId = sectorId });
        }

        _fixture.DbContext.GateDevices.Add(device);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Re-read the way the authentication handler does, so the entity under test is the same
        // shape production hands to the service — sectors loaded, tracked.
        return (await _fixture.GateDeviceRepository.GetByKeyHashAsync(key.KeyHash))!;
    }

    private async Task<Ticket> ReloadAsync(Guid ticketId)
    {
        _fixture.DbContext.ChangeTracker.Clear();
        return (await _fixture.DbContext.Tickets.FindAsync(ticketId))!;
    }

    private ClaimsPrincipal OrganizerOf(Guid organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "OrganizationAdmin"),
            new(ClaimTypes.Email, "gate@example.com"),
            new("organizationId", organizationId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private ClaimsPrincipal PlatformStaff()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _scannerUserId.ToString()),
            new(ClaimTypes.Role, "SuperAdmin"),
            new(ClaimTypes.Email, "staff@example.com"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
