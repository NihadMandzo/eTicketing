using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using FluentAssertions;

namespace eTicketing.Identity.Business.Tests.Admins;

public class AdminServiceTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IAdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(_fixture.UserRepository, _fixture.UnitOfWork);
    }

    private async Task<User> SeedUserAsync(RoleType role, string email)
    {
        var (hash, salt) = PasswordHasher.Hash("SomePassword123");
        var user = new User
        {
            FirstName = "Seed",
            LastName = "User",
            Email = email,
            Username = email.Split('@')[0],
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false
        };

        await _fixture.UserRepository.AddAsync(user);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetAsync_WithNoRoleFilter_ReturnsAllNonUserRoles()
    {
        // UserSeeder.cs (HasData, applied by EnsureCreated() too) already seeds 4 non-User staff
        // accounts — same reasoning as OrganizationServiceTests' seeded-organizations comments —
        // so this asserts the delta the 4 newly-seeded accounts make, not an absolute count.
        var before = await _sut.GetAsync(new AdminQuery());

        await SeedUserAsync(RoleType.SuperAdmin, "super@example.com");
        await SeedUserAsync(RoleType.Admin, "admin1@example.com");
        await SeedUserAsync(RoleType.OrganizationSuperAdmin, "orgsuper@example.com");
        await SeedUserAsync(RoleType.OrganizationAdmin, "orgadmin@example.com");
        await SeedUserAsync(RoleType.User, "buyer@example.com");

        var result = await _sut.GetAsync(new AdminQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(before.Value!.TotalCount + 4);
        result.Value.Items.Should().NotContain(u => u.RoleName == "User");
    }

    [Fact]
    public async Task GetAsync_WithSingleRoleFilter_ReturnsOnlyThatRole()
    {
        await SeedUserAsync(RoleType.Admin, "admin2@example.com");
        await SeedUserAsync(RoleType.OrganizationAdmin, "orgadmin2@example.com");

        var result = await _sut.GetAsync(new AdminQuery { RoleFilters = [RoleType.Admin] });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(u => u.RoleName == "Admin");
    }

    [Fact]
    public async Task GetAsync_WithMultipleRoleFilters_ReturnsUnionOfThoseRoles()
    {
        await SeedUserAsync(RoleType.Admin, "admin3@example.com");
        await SeedUserAsync(RoleType.OrganizationAdmin, "orgadmin3@example.com");
        await SeedUserAsync(RoleType.OrganizationSuperAdmin, "orgsuper3@example.com");

        var result = await _sut.GetAsync(new AdminQuery { RoleFilters = [RoleType.Admin, RoleType.OrganizationAdmin] });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().Contain(u => u.Email == "admin3@example.com");
        result.Value.Items.Should().Contain(u => u.Email == "orgadmin3@example.com");
        result.Value.Items.Should().NotContain(u => u.Email == "orgsuper3@example.com");
    }

    [Fact]
    public async Task GetAsync_WithEmptyRoleFiltersArray_IsTreatedAsNoFilter()
    {
        // Documents the same minimal-API binding quirk OrganizationQuery.OrganizationIds
        // already works around: an *absent* array-typed query param binds to an empty array,
        // not null, so an empty (but non-null) RoleFilters must mean "no filter".
        var before = await _sut.GetAsync(new AdminQuery());
        await SeedUserAsync(RoleType.Admin, "admin4@example.com");

        var result = await _sut.GetAsync(new AdminQuery { RoleFilters = [] });

        result.Value!.TotalCount.Should().Be(before.Value!.TotalCount + 1);
    }

    [Fact]
    public async Task GetAsync_ExcludesUsersWithRoleUser()
    {
        var before = await _sut.GetAsync(new AdminQuery());
        await SeedUserAsync(RoleType.User, "buyer2@example.com");

        var result = await _sut.GetAsync(new AdminQuery());

        // A buyer joining doesn't change the staff count at all — SearchStaffAsync excludes them.
        result.Value!.TotalCount.Should().Be(before.Value!.TotalCount);
        result.Value.Items.Should().NotContain(u => u.Email == "buyer2@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ForOrganizationSuperAdmin_ReturnsSuccess()
    {
        var orgSuperAdmin = await SeedUserAsync(RoleType.OrganizationSuperAdmin, "orgsuper2@example.com");

        var result = await _sut.GetByIdAsync(orgSuperAdmin.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoleName.Should().Be("OrganizationSuperAdmin");
    }

    [Fact]
    public async Task GetByIdAsync_ForUserRole_ReturnsNotFound()
    {
        var buyer = await SeedUserAsync(RoleType.User, "buyer3@example.com");

        var result = await _sut.GetByIdAsync(buyer.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("admin.not_found");
    }

    [Fact]
    public async Task GetByIdAsync_ForUnknownGuid_ReturnsNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ThenGetById_RoundTrips()
    {
        var created = await _sut.CreateAsync(new CreateAdminRequest
        {
            FirstName = "New",
            LastName = "Admin",
            Email = "new.admin@example.com",
            Username = "newadmin",
            Password = "SuperSecret123"
        });

        created.IsSuccess.Should().BeTrue();
        created.Value!.RoleName.Should().Be("Admin");

        var fetched = await _sut.GetByIdAsync(created.Value.Id);
        fetched.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesProfileFields()
    {
        var admin = await SeedUserAsync(RoleType.Admin, "update.me@example.com");

        var result = await _sut.UpdateAsync(admin.Id, new UpdateStaffUserRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated.email@example.com",
            Username = "updatedusername",
            PhoneNumber = "+387 61 111 222"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("Updated");
        result.Value.Email.Should().Be("updated.email@example.com");
        result.Value.Username.Should().Be("updatedusername");
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateEmail_ReturnsConflict()
    {
        await SeedUserAsync(RoleType.Admin, "taken@example.com");
        var admin = await SeedUserAsync(RoleType.Admin, "mine@example.com");

        var result = await _sut.UpdateAsync(admin.Id, new UpdateStaffUserRequest
        {
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            Email = "taken@example.com",
            Username = admin.Username
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.already_exists");
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateUsername_ReturnsConflict()
    {
        await SeedUserAsync(RoleType.Admin, "other@example.com");
        var admin = await SeedUserAsync(RoleType.Admin, "mine2@example.com");

        var result = await _sut.UpdateAsync(admin.Id, new UpdateStaffUserRequest
        {
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            Email = admin.Email,
            Username = "other" // SeedUserAsync derives username from email's local part
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.already_exists");
    }

    [Fact]
    public async Task UpdateAsync_KeepingOwnEmailAndUsername_Succeeds()
    {
        var admin = await SeedUserAsync(RoleType.Admin, "keepmine@example.com");

        var result = await _sut.UpdateAsync(admin.Id, new UpdateStaffUserRequest
        {
            FirstName = "Changed",
            LastName = admin.LastName,
            Email = admin.Email,
            Username = admin.Username
        });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ForUnknownGuid_ReturnsNotFound()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateStaffUserRequest
        {
            FirstName = "Ghost",
            LastName = "User",
            Email = "ghost@example.com",
            Username = "ghostuser"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("admin.not_found");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheAdmin()
    {
        var created = await _sut.CreateAsync(new CreateAdminRequest
        {
            FirstName = "ToDelete",
            LastName = "Admin",
            Email = "delete.me@example.com",
            Username = "deleteme",
            Password = "SuperSecret123"
        });

        var deleteResult = await _sut.DeleteAsync(created.Value!.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        var afterDelete = await _sut.GetByIdAsync(created.Value.Id);
        afterDelete.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForOrganizationSuperAdmin_ReturnsConflict()
    {
        var orgSuperAdmin = await SeedUserAsync(RoleType.OrganizationSuperAdmin, "cantdelete@example.com");

        var result = await _sut.DeleteAsync(orgSuperAdmin.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.super_admin_required");
    }

    [Fact]
    public async Task DeleteAsync_ForOrganizationAdmin_Succeeds()
    {
        var orgAdmin = await SeedUserAsync(RoleType.OrganizationAdmin, "candelete@example.com");

        var result = await _sut.DeleteAsync(orgAdmin.Id);

        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose() => _fixture.Dispose();
}
