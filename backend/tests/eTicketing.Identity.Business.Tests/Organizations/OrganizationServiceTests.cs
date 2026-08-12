using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Identity.Business.Tests.Organizations;

public class OrganizationServiceTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IOrganizationService _sut;

    public OrganizationServiceTests()
    {
        _sut = _fixture.CreateOrganizationService();
    }

    private static CreateOrganizationRequest ValidCreateRequest() => new()
    {
        Name = "Acme Events",
        Description = "We throw great events.",
        Address = "123 Main St",
        PhoneNumber = "+387 61 000 000",
        Email = "info@acme.example.com",
        Website = "https://acme.example.com",
        AdminFirstName = "Alice",
        AdminLastName = "Admin",
        AdminEmail = "alice@acme.example.com",
        AdminUsername = "aliceadmin",
        AdminPassword = "SuperSecret123",
        AdminRole = RoleType.OrganizationSuperAdmin
    };

    [Fact]
    public async Task CreateAsync_CreatesOrganizationAndFirstAdminInOneTransaction()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserCount.Should().Be(1);

        var user = await _fixture.UserRepository.GetByEmailAsync("alice@acme.example.com");
        user.Should().NotBeNull();
        user!.Role.Should().Be(RoleType.OrganizationSuperAdmin);
        user.OrganizationId.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ForUnknownGuid_ReturnsNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.not_found");
    }

    [Fact]
    public async Task AddUserAsync_AddsUserScopedToOrganization()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());

        var added = await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin
        });

        added.IsSuccess.Should().BeTrue();
        added.Value!.OrganizationId.Should().Be(org.Value.Id);
    }

    [Fact]
    public async Task RemoveUserAsync_ForUserInDifferentOrganization_ReturnsNotFound()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        var orgB = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "carol@other.example.com",
            AdminUsername = "carolother"
        });

        var userInOrgA = await _fixture.UserRepository.GetByEmailAsync("alice@acme.example.com");

        var result = await _sut.RemoveUserAsync(orgB.Value!.Id, userInOrgA!.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.not_found");
    }

    [Fact]
    public async Task GetAsync_WithOrganizationIds_ReturnsOnlyMatchingOrganizations()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "other@acme.example.com",
            AdminUsername = "otheradmin"
        });

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = [orgA.Value!.Id] });

        result.Value!.Items.Should().ContainSingle(o => o.Id == orgA.Value.Id);
    }

    [Fact]
    public async Task GetAsync_WithNullOrganizationIds_ReturnsAllOrganizations()
    {
        await _sut.CreateAsync(ValidCreateRequest());
        await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "other@acme.example.com",
            AdminUsername = "otheradmin"
        });

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = null });

        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_WithEmptyOrganizationIdsArray_IsTreatedAsNoFilter()
    {
        // Deliberately documents the minimal-API binding quirk this repository query works
        // around: an *absent* array-typed query param binds to an empty array, not null — so
        // an empty (but non-null) OrganizationIds must mean "no filter", not "match nothing".
        // The frontend never sends a genuinely-empty array to mean "match nothing" — it
        // short-circuits locally instead (see OrganizationsScreen._loadData()) — but the
        // repository still needs to treat this input this way for the "filter not in use at
        // all" case to keep working.
        await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = [] });

        result.Value!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByRole()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Admin", Email = "bob@acme.example.com",
            Username = "bobadmin", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        });

        var result = await _sut.GetUsersAsync(org.Value.Id, new OrganizationUserQuery { Role = RoleType.OrganizationSuperAdmin });

        // The org's own first admin (Alice, from CreateAsync) is OrganizationSuperAdmin — only
        // she should come back, not Bob (OrganizationAdmin).
        result.Value!.Items.Should().ContainSingle(u => u.Email == "alice@acme.example.com");
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByFts()
    {
        // Regression test for the bug this task fixed: SearchByOrganizationAsync used to never
        // apply query.FTS at all, so this filter silently had no effect.
        var org = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        });

        var result = await _sut.GetUsersAsync(org.Value.Id, new OrganizationUserQuery { FTS = "Bob" });

        result.Value!.Items.Should().ContainSingle(u => u.Email == "bob@acme.example.com");
    }

    [Fact]
    public async Task GetUsersAsync_WithRoleAndFtsCombined_UsesAndSemantics()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        });

        // Bob is OrganizationAdmin, not OrganizationSuperAdmin — searching for his name while
        // filtering to the wrong role must return nothing, not ignore one of the two filters.
        var result = await _sut.GetUsersAsync(org.Value.Id,
            new OrganizationUserQuery { Role = RoleType.OrganizationSuperAdmin, FTS = "Bob" });

        result.Value!.Items.Should().BeEmpty();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static IFormFile CreateLogoFile(byte[] bytes, string contentType = "image/png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "Logo", "logo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    [Fact]
    public async Task CreateAsync_WithLogo_StoresBytesAndBuildsLogoUrl()
    {
        var logoBytes = CreatePngBytes(80, 80);

        var result = await _sut.CreateAsync(ValidCreateRequest() with { Logo = CreateLogoFile(logoBytes) });

        result.IsSuccess.Should().BeTrue();
        result.Value!.LogoUrl.Should().Be($"http://localhost:5000/api/organizations/{result.Value.Id}/logo");

        var logo = await _sut.GetLogoAsync(result.Value.Id);
        logo.IsSuccess.Should().BeTrue();
        logo.Value!.Data.Should().BeEquivalentTo(logoBytes);
    }

    [Fact]
    public async Task CreateAsync_WithoutLogo_LogoUrlIsNull()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.Value!.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithoutLogoOrRemoveLogo_KeepsExistingLogo()
    {
        var originalLogo = CreatePngBytes(60, 60);
        var created = await _sut.CreateAsync(ValidCreateRequest() with { Logo = CreateLogoFile(originalLogo) });

        var updated = await _sut.UpdateAsync(created.Value!.Id, new UpdateOrganizationRequest
        {
            Name = "Acme Events Renamed",
            Description = "Updated",
            Address = "456 Other St",
            PhoneNumber = "+387 61 111 111",
            Email = "info@acme.example.com",
            IsActive = true,
        });

        updated.Value!.LogoUrl.Should().NotBeNull();
        var logo = await _sut.GetLogoAsync(created.Value.Id);
        logo.Value!.Data.Should().BeEquivalentTo(originalLogo);
    }

    [Fact]
    public async Task UpdateAsync_WithRemoveLogoTrue_ClearsLogo()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest() with { Logo = CreateLogoFile(CreatePngBytes(60, 60)) });

        var updated = await _sut.UpdateAsync(created.Value!.Id, new UpdateOrganizationRequest
        {
            Name = "Acme Events",
            Description = "Updated",
            Address = "456 Other St",
            PhoneNumber = "+387 61 111 111",
            Email = "info@acme.example.com",
            IsActive = true,
            RemoveLogo = true,
        });

        updated.Value!.LogoUrl.Should().BeNull();
        var logo = await _sut.GetLogoAsync(created.Value.Id);
        logo.IsFailure.Should().BeTrue();
        logo.Error.Code.Should().Be("organization.logo_not_found");
    }

    [Fact]
    public async Task UpdateAsync_WithNewLogo_ReplacesExistingLogo()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest() with { Logo = CreateLogoFile(CreatePngBytes(40, 40)) });
        var replacementLogo = CreatePngBytes(90, 90);

        await _sut.UpdateAsync(created.Value!.Id, new UpdateOrganizationRequest
        {
            Name = "Acme Events",
            Description = "Updated",
            Address = "456 Other St",
            PhoneNumber = "+387 61 111 111",
            Email = "info@acme.example.com",
            IsActive = true,
            Logo = CreateLogoFile(replacementLogo),
        });

        var logo = await _sut.GetLogoAsync(created.Value.Id);
        logo.Value!.Data.Should().BeEquivalentTo(replacementLogo);
    }

    [Fact]
    public async Task GetLogoAsync_ForUnknownOrganization_ReturnsNotFound()
    {
        var result = await _sut.GetLogoAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.logo_not_found");
    }

    public void Dispose() => _fixture.Dispose();
}
