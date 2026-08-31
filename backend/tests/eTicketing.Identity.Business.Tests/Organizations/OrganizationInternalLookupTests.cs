using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Tests.TestFixtures;
using FluentAssertions;

namespace eTicketing.Identity.Business.Tests.Organizations;

/// <summary>
/// POST /internal/organizations/by-ids — how eTicketing.Ticketing's Izvještaji reports put a name
/// on an organization id. Internal-only, so there is no caller/ownership check to test: the
/// interesting behaviour is what it does with ids it cannot resolve, and the cap that keeps the
/// id list bounded.
/// </summary>
public class OrganizationInternalLookupTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IOrganizationService _sut;

    public OrganizationInternalLookupTests() => _sut = _fixture.CreateOrganizationService();

    private static CreateOrganizationRequest CreateRequest(string name, string address) => new()
    {
        Name = name,
        Description = "Opis organizacije.",
        Address = address,
        PhoneNumber = "+387 61 000 000",
        Email = $"info@{name.ToLowerInvariant().Replace(" ", "")}.example.com",
        Website = "https://example.com",
        AdminFirstName = "Adnan",
        AdminLastName = "Hodžić",
        AdminEmail = $"admin@{name.ToLowerInvariant().Replace(" ", "")}.example.com",
        AdminUsername = name.ToLowerInvariant().Replace(" ", ""),
        AdminPassword = "SuperSecret123",
    };

    private async Task<Guid> SeedOrganizationAsync(string name, string address)
    {
        var created = await _sut.CreateAsync(CreateRequest(name, address));
        created.IsSuccess.Should().BeTrue();
        return created.Value!.Id;
    }

    [Fact]
    public async Task GetInternalByIdsAsync_ReturnsNameAndAddressForEachKnownId()
    {
        var sunset = await SeedOrganizationAsync("Sunset Events", "Kralja Petra 1, Mostar");
        var jazz = await SeedOrganizationAsync("Jazz Centar", "Ferhadija 5, Sarajevo");

        var result = await _sut.GetInternalByIdsAsync([sunset, jazz]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Single(o => o.Id == sunset).Name.Should().Be("Sunset Events");
        result.Value.Single(o => o.Id == sunset).Address.Should().Be("Kralja Petra 1, Mostar");
        result.Value.Single(o => o.Id == jazz).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetInternalByIdsAsync_SkipsUnknownIdsRatherThanFailing()
    {
        var known = await SeedOrganizationAsync("Sunset Events", "Kralja Petra 1, Mostar");

        // An organization deleted between a sale and the report that counts it must not take the
        // whole page down with it.
        var result = await _sut.GetInternalByIdsAsync([known, Guid.NewGuid()]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.Id.Should().Be(known);
    }

    [Fact]
    public async Task GetInternalByIdsAsync_ForAnEmptyIdList_ReturnsAnEmptyResult()
    {
        var result = await _sut.GetInternalByIdsAsync([]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInternalByIdsAsync_BeyondTheIdCap_ReturnsValidationFailure()
    {
        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        var result = await _sut.GetInternalByIdsAsync(tooMany);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("organization.too_many_ids");
    }

    public void Dispose() => _fixture.Dispose();
}
