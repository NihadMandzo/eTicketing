using System.Security.Claims;
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
        AdminPassword = "SuperSecret123"
    };

    /// <summary>Builds a ClaimsPrincipal matching the claim shape JwtTokenGenerator mints
    /// (ClaimTypes.NameIdentifier/Role + a plain "organizationId" claim), for exercising
    /// OrganizationService's ownership checks without going through real JWT issuance.</summary>
    private static ClaimsPrincipal BuildCaller(RoleType role, Guid? organizationId = null, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimTypes.Role, role.ToString()),
        };
        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static ClaimsPrincipal PlatformStaffCaller() => BuildCaller(RoleType.SuperAdmin);

    [Fact]
    public async Task CreateAsync_FirstAdminIsAlwaysOrganizationSuperAdmin()
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
    public async Task AddUserAsync_ByPlatformStaff_ForAnyOrg_Succeeds()
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
        }, PlatformStaffCaller());

        added.IsSuccess.Should().BeTrue();
        added.Value!.OrganizationId.Should().Be(org.Value.Id);
    }

    [Fact]
    public async Task AddUserAsync_ByOrganizationSuperAdmin_ForOwnOrg_CreatesOrganizationAdmin()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var alice = await _fixture.UserRepository.GetByEmailAsync("alice@acme.example.com");
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, org.Value!.Id, alice!.Id);

        var result = await _sut.AddUserAsync(org.Value.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin
        }, caller);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoleName.Should().Be("OrganizationAdmin");
    }

    [Fact]
    public async Task AddUserAsync_ByOrganizationSuperAdmin_ForOtherOrg_ReturnsForbidden()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        var orgB = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "carol@other.example.com",
            AdminUsername = "carolother"
        });
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, orgB.Value!.Id);

        var result = await _sut.AddUserAsync(orgA.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin
        }, caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.forbidden");
    }

    [Fact]
    public async Task AddUserAsync_ByOrganizationSuperAdmin_AttemptingSuperAdminRole_ReturnsValidationError()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, org.Value!.Id);

        var result = await _sut.AddUserAsync(org.Value.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationSuperAdmin
        }, caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.role_not_allowed");
    }

    [Fact]
    public async Task AddUserAsync_ByOrganizationAdmin_ReturnsForbidden()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var caller = BuildCaller(RoleType.OrganizationAdmin, org.Value!.Id);

        var result = await _sut.AddUserAsync(org.Value.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin
        }, caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.forbidden");
    }

    [Fact]
    public async Task AddUserAsync_SecondOrganizationSuperAdminForSameOrg_ReturnsConflict()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Carl",
            LastName = "Second",
            Email = "carl@acme.example.com",
            Username = "carlsecond",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationSuperAdmin
        }, PlatformStaffCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.super_admin_already_exists");
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

        var result = await _sut.RemoveUserAsync(orgB.Value!.Id, userInOrgA!.Id, PlatformStaffCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.not_found");
    }

    [Fact]
    public async Task RemoveUserAsync_ByOrganizationSuperAdmin_OwnOrgOrganizationAdmin_Succeeds()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var added = await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, org.Value.Id);

        var result = await _sut.RemoveUserAsync(org.Value.Id, added.Value!.Id, caller);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveUserAsync_ByOrganizationSuperAdmin_OtherOrg_ReturnsForbidden()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        var orgB = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "carol@other.example.com",
            AdminUsername = "carolother"
        });
        var added = await _sut.AddUserAsync(orgA.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, orgB.Value!.Id);

        var result = await _sut.RemoveUserAsync(orgA.Value.Id, added.Value!.Id, caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.forbidden");
    }

    [Fact]
    public async Task RemoveUserAsync_TargetingOrganizationSuperAdmin_ReturnsConflict()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var alice = await _fixture.UserRepository.GetByEmailAsync("alice@acme.example.com");

        var result = await _sut.RemoveUserAsync(org.Value!.Id, alice!.Id, PlatformStaffCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.super_admin_required");
    }

    [Fact]
    public async Task UpdateUserAsync_ByOrganizationSuperAdmin_OwnOrgOrganizationAdmin_UpdatesFields()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var added = await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, org.Value.Id);

        var result = await _sut.UpdateUserAsync(org.Value.Id, added.Value!.Id, new UpdateOrganizationUserRequest
        {
            FirstName = "Robert",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            PhoneNumber = "+387 61 222 333"
        }, caller);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("Robert");
    }

    [Fact]
    public async Task UpdateUserAsync_ByOrganizationSuperAdmin_OtherOrg_ReturnsForbidden()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        var orgB = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "carol@other.example.com",
            AdminUsername = "carolother"
        });
        var added = await _sut.AddUserAsync(orgA.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());
        var caller = BuildCaller(RoleType.OrganizationSuperAdmin, orgB.Value!.Id);

        var result = await _sut.UpdateUserAsync(orgA.Value.Id, added.Value!.Id, new UpdateOrganizationUserRequest
        {
            FirstName = "Robert",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff"
        }, caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.forbidden");
    }

    [Fact]
    public async Task UpdateUserAsync_DuplicateEmail_ReturnsConflict()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var added = await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Staff", Email = "bob@acme.example.com",
            Username = "bobstaff", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());

        var result = await _sut.UpdateUserAsync(org.Value.Id, added.Value!.Id, new UpdateOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "alice@acme.example.com", // Alice's (the org superadmin's) email — already taken
            Username = "bobstaff"
        }, PlatformStaffCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.already_exists");
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
        // 2 created here + the 2 fixed organizations from OrganizationSeeder.cs (HasData,
        // applied by EnsureCreated() too) — a null filter means "no filter", so all 4 come back.
        await _sut.CreateAsync(ValidCreateRequest());
        await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "other@acme.example.com",
            AdminUsername = "otheradmin"
        });

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = null });

        result.Value!.Items.Should().HaveCount(4);
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
        // all" case to keep working. 1 created here + the 2 fixed seeded organizations = 3.
        await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = [] });

        result.Value!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByRole()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddUserAsync(org.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob", LastName = "Admin", Email = "bob@acme.example.com",
            Username = "bobadmin", Password = "SuperSecret123", Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());

        var result = await _sut.GetUsersAsync(org.Value.Id, new OrganizationUserQuery { Role = RoleType.OrganizationSuperAdmin }, PlatformStaffCaller());

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
        }, PlatformStaffCaller());

        var result = await _sut.GetUsersAsync(org.Value.Id, new OrganizationUserQuery { FTS = "Bob" }, PlatformStaffCaller());

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
        }, PlatformStaffCaller());

        // Bob is OrganizationAdmin, not OrganizationSuperAdmin — searching for his name while
        // filtering to the wrong role must return nothing, not ignore one of the two filters.
        var result = await _sut.GetUsersAsync(org.Value.Id,
            new OrganizationUserQuery { Role = RoleType.OrganizationSuperAdmin, FTS = "Bob" }, PlatformStaffCaller());

        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersAsync_ByOrganizationAdmin_OwnOrg_Succeeds()
    {
        var org = await _sut.CreateAsync(ValidCreateRequest());
        var caller = BuildCaller(RoleType.OrganizationAdmin, org.Value!.Id);

        var result = await _sut.GetUsersAsync(org.Value.Id, new OrganizationUserQuery(), caller);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsersAsync_ByUnrelatedOrgUser_ReturnsForbidden()
    {
        var orgA = await _sut.CreateAsync(ValidCreateRequest());
        var orgB = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "carol@other.example.com",
            AdminUsername = "carolother"
        });
        var caller = BuildCaller(RoleType.OrganizationAdmin, orgB.Value!.Id);

        var result = await _sut.GetUsersAsync(orgA.Value!.Id, new OrganizationUserQuery(), caller);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.forbidden");
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
    public async Task CreateAsync_LogoUrlIsNullUntilUploaded()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.Value!.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task UploadLogoAsync_ForOrganizationWithoutLogo_StoresBlobAndReturnsUrl()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.UploadLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(80, 80)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.LogoUrl.Should().NotBeNull();
        result.Value.LogoUrl.Should().Contain("organization-logos");
    }

    [Fact]
    public async Task UploadLogoAsync_ForOrganizationThatAlreadyHasLogo_ReturnsConflict()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(60, 60)));

        var result = await _sut.UploadLogoAsync(created.Value.Id, CreateLogoFile(CreatePngBytes(60, 60)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.logo_already_exists");
    }

    [Fact]
    public async Task UploadLogoAsync_ForUnknownOrganization_ReturnsNotFound()
    {
        var result = await _sut.UploadLogoAsync(Guid.NewGuid(), CreateLogoFile(CreatePngBytes(60, 60)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.not_found");
    }

    [Fact]
    public async Task ReplaceLogoAsync_ForOrganizationWithLogo_OverwritesSameBlobKey()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var uploaded = await _sut.UploadLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(40, 40)));
        var originalUrl = uploaded.Value!.LogoUrl;

        var replaced = await _sut.ReplaceLogoAsync(created.Value.Id, CreateLogoFile(CreatePngBytes(90, 90)));

        replaced.IsSuccess.Should().BeTrue();
        replaced.Value!.LogoUrl.Should().Be(originalUrl); // same blob key, just overwritten
    }

    [Fact]
    public async Task ReplaceLogoAsync_ForOrganizationWithoutLogo_ReturnsNotFound()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.ReplaceLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.logo_not_found");
    }

    [Fact]
    public async Task ReplaceLogoAsync_ForUnknownOrganization_ReturnsNotFound()
    {
        var result = await _sut.ReplaceLogoAsync(Guid.NewGuid(), CreateLogoFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.not_found");
    }

    [Fact]
    public async Task DeleteAsync_ForOrganizationWithLogo_DeletesBlobToo()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var uploaded = await _sut.UploadLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(50, 50)));
        var blobName = uploaded.Value!.LogoUrl!.Split('/').Last();
        _fixture.BlobStorage.Exists("organization-logos", blobName).Should().BeTrue();

        await _sut.DeleteAsync(created.Value.Id);

        _fixture.BlobStorage.Exists("organization-logos", blobName).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenBlobDeleteFails_StillRemovesOrganizationRow()
    {
        // Regression test for the fix this task made: the blob is now deleted only after the DB
        // delete commits, so a blob-storage failure (e.g. transient Azure outage) can no longer
        // leave the organization row alive while pointing at an already-deleted blob.
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadLogoAsync(created.Value!.Id, CreateLogoFile(CreatePngBytes(50, 50)));
        _fixture.BlobStorage.ThrowOnDelete = true;

        var act = () => _sut.DeleteAsync(created.Value.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        var fetched = await _sut.GetByIdAsync(created.Value.Id);
        fetched.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForOrganizationWithUsers_CascadeDeletesAllOfItsUsers()
    {
        // Every organization has at least one user (the admin created alongside it in
        // CreateAsync) — add a second so this also covers users beyond the first admin.
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddUserAsync(created.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Staff",
            Email = "bob@acme.example.com",
            Username = "bobstaff",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin
        }, PlatformStaffCaller());

        var result = await _sut.DeleteAsync(created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        (await _fixture.UserRepository.GetByEmailAsync("alice@acme.example.com")).Should().BeNull();
        (await _fixture.UserRepository.GetByEmailAsync("bob@acme.example.com")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsCorrectLogoUrlPresenceForEachOrganization()
    {
        var withLogo = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadLogoAsync(withLogo.Value!.Id, CreateLogoFile(CreatePngBytes(50, 50)));
        var withoutLogo = await _sut.CreateAsync(ValidCreateRequest() with
        {
            AdminEmail = "other@acme.example.com",
            AdminUsername = "otheradmin"
        });

        var result = await _sut.GetAsync(new OrganizationQuery { OrganizationIds = [withLogo.Value.Id, withoutLogo.Value!.Id] });

        result.Value!.Items.Should().ContainSingle(o => o.Id == withLogo.Value.Id && o.LogoUrl != null);
        result.Value.Items.Should().ContainSingle(o => o.Id == withoutLogo.Value.Id && o.LogoUrl == null);
    }

    public void Dispose() => _fixture.Dispose();
}
