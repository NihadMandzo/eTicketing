using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.GateDevices.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.GateDevices;

public class UpsertGateDeviceRequestValidatorTests
{
    private readonly UpsertGateDeviceRequestValidator _sut = new();

    private static UpsertGateDeviceRequest Valid(
        bool allSectors = false, List<Guid>? sectorIds = null, string name = "Ulaz A", Guid? productId = null) =>
        new()
        {
            ProductId = productId ?? Guid.NewGuid(),
            Name = name,
            AllSectors = allSectors,
            SectorIds = sectorIds ?? [Guid.NewGuid()],
        };

    [Fact]
    public void Validate_ForAWellFormedScopedRequest_Passes()
    {
        _sut.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ForAnAllSectorsRequestWithNoSectorIds_Passes()
    {
        _sut.Validate(Valid(allSectors: true, sectorIds: [])).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithABlankName_Fails(string name)
    {
        var result = _sut.Validate(Valid(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Naziv uređaja je obavezan.");
    }

    [Fact]
    public void Validate_WithAnOverlongName_Fails()
    {
        var result = _sut.Validate(Valid(name: new string('x', UpsertGateDeviceRequestValidator.MaxNameLength + 1)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithAnEmptyProductId_Fails()
    {
        var result = _sut.Validate(Valid(productId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Proizvod je obavezan.");
    }

    [Fact]
    public void Validate_WithNeitherSectorsNorAllSectors_Fails()
    {
        // The case the whole AllSectors flag exists to make impossible to reach by accident.
        var result = _sut.Validate(Valid(sectorIds: []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Odaberite najmanje jedan sektor ili uključite opciju 'Svi sektori'.");
    }

    [Fact]
    public void Validate_WithAnEmptyGuidAmongTheSectors_Fails()
    {
        var result = _sut.Validate(Valid(sectorIds: [Guid.NewGuid(), Guid.Empty]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Lista sektora sadrži neispravan identifikator.");
    }

    [Fact]
    public void Validate_WithTheSameSectorTwice_Fails()
    {
        var sectorId = Guid.NewGuid();

        var result = _sut.Validate(Valid(sectorIds: [sectorId, sectorId]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Isti sektor je odabran više puta.");
    }

    [Fact]
    public void Validate_WithMoreSectorsThanTheCap_Fails()
    {
        var tooMany = Enumerable.Range(0, UpsertGateDeviceRequestValidator.MaxSectors + 1)
            .Select(_ => Guid.NewGuid()).ToList();

        _sut.Validate(Valid(sectorIds: tooMany)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AtExactlyTheSectorCap_Passes()
    {
        var atCap = Enumerable.Range(0, UpsertGateDeviceRequestValidator.MaxSectors)
            .Select(_ => Guid.NewGuid()).ToList();

        _sut.Validate(Valid(sectorIds: atCap)).IsValid.Should().BeTrue();
    }
}
