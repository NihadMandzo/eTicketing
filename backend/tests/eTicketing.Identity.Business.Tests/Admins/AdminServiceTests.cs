using eTicketing.Contracts.Events;
using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using FluentAssertions;
using Moq;

namespace eTicketing.Identity.Business.Tests.Admins;

public class AdminServiceTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IAdminService _sut;

    public AdminServiceTests()
    {
        _sut = _fixture.CreateAdminService();
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

        var deleteResult = await _sut.DeleteAsync(created.Value!.Id, new DeleteAdminRequest());
        deleteResult.IsSuccess.Should().BeTrue();

        var afterDelete = await _sut.GetByIdAsync(created.Value.Id);
        afterDelete.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForOrganizationSuperAdmin_ReturnsConflict()
    {
        var orgSuperAdmin = await SeedUserAsync(RoleType.OrganizationSuperAdmin, "cantdelete@example.com");

        var result = await _sut.DeleteAsync(orgSuperAdmin.Id, new DeleteAdminRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("organization.super_admin_required");
    }

    [Fact]
    public async Task DeleteAsync_OrganizationAdminWithReason_PublishesOrganizationAdminDeletedEvent()
    {
        var org = await SeedOrganizationAsync("Acme Events");
        var orgAdmin = await SeedUserAsync(RoleType.OrganizationAdmin, "candelete@example.com", org.Id);

        var result = await _sut.DeleteAsync(orgAdmin.Id, new DeleteAdminRequest
        {
            Reason = "Kršenje internih pravila organizacije.",
            RecipientEmail = "kontakt@acme.example"
        });

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisherMock.Verify(p => p.PublishAsync(
            EventNames.OrganizationAdminDeleted,
            It.Is<OrganizationAdminDeletedNotification>(n =>
                n.OrganizationId == org.Id
                && n.OrganizationName == "Acme Events"
                && n.RecipientEmail == "kontakt@acme.example"
                && n.Reason == "Kršenje internih pravila organizacije."
                && n.DeletedAdminFullName == $"{orgAdmin.FirstName} {orgAdmin.LastName}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "kontakt@acme.example")]
    [InlineData("Razlog", null)]
    [InlineData("", "")]
    public async Task DeleteAsync_OrganizationAdminWithoutReasonOrEmail_ReturnsValidationError(string? reason, string? recipientEmail)
    {
        var org = await SeedOrganizationAsync("Missing Fields Org");
        var orgAdmin = await SeedUserAsync(RoleType.OrganizationAdmin, "incomplete@example.com", org.Id);

        var result = await _sut.DeleteAsync(orgAdmin.Id, new DeleteAdminRequest { Reason = reason, RecipientEmail = recipientEmail });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("admin.delete_reason_required");
        // PublishAsync<T> is generic — It.IsAny<object>() would only match a PublishAsync<object>
        // call (a different closed generic method from whatever concrete T the real code uses),
        // so "never called with any T" is asserted via the mock's raw invocation list instead of
        // a (silently-wrong) Verify() against one specific closed generic signature.
        _fixture.EventPublisherMock.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_PlatformStaffTarget_DeletesWithoutPublishingEvent()
    {
        var admin = await SeedUserAsync(RoleType.Admin, "platformstaff@example.com");

        var result = await _sut.DeleteAsync(admin.Id, new DeleteAdminRequest());

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisherMock.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task SetPasswordAsync_ForOrganizationAdmin_SetsMustChangePasswordAndPublishesEvent()
    {
        var orgAdmin = await SeedUserAsync(RoleType.OrganizationAdmin, "setpwd@example.com");
        var originalHash = orgAdmin.PasswordHash;

        var result = await _sut.SetPasswordAsync(orgAdmin.Id, new SetPasswordRequest
        {
            NewPassword = "BrandNewPassword123",
            ConfirmPassword = "BrandNewPassword123"
        });

        result.IsSuccess.Should().BeTrue();

        var updated = await _fixture.UserRepository.GetByIdAsync(orgAdmin.Id);
        updated!.MustChangePassword.Should().BeTrue();
        updated.PasswordHash.Should().NotBe(originalHash);

        _fixture.EventPublisherMock.Verify(p => p.PublishAsync(
            EventNames.AdminPasswordChanged,
            It.Is<AdminPasswordChangedNotification>(n => n.UserId == orgAdmin.Id && n.Email == orgAdmin.Email),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPasswordAsync_ForUserRole_ReturnsNotFound()
    {
        var buyer = await SeedUserAsync(RoleType.User, "buyer.setpwd@example.com");

        var result = await _sut.SetPasswordAsync(buyer.Id, new SetPasswordRequest
        {
            NewPassword = "BrandNewPassword123",
            ConfirmPassword = "BrandNewPassword123"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("admin.not_found");
    }

    [Fact]
    public async Task SetPasswordAsync_ForSuperAdminRole_ReturnsNotFound()
    {
        var superAdmin = await SeedUserAsync(RoleType.SuperAdmin, "peer.superadmin@example.com");

        var result = await _sut.SetPasswordAsync(superAdmin.Id, new SetPasswordRequest
        {
            NewPassword = "BrandNewPassword123",
            ConfirmPassword = "BrandNewPassword123"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("admin.not_found");
    }

    private async Task<Organization> SeedOrganizationAsync(string name)
    {
        var organization = new Organization
        {
            Name = name,
            Description = "Test organization",
            Address = "Test Address 1",
            PhoneNumber = "+387 61 000 000",
            Email = $"{name.Replace(" ", "").ToLowerInvariant()}@example.com"
        };
        await _fixture.OrganizationRepository.AddAsync(organization);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return organization;
    }

    private async Task<User> SeedUserAsync(RoleType role, string email, Guid organizationId)
    {
        var user = await SeedUserAsync(role, email);
        user.OrganizationId = organizationId;
        await _fixture.UnitOfWork.SaveChangesAsync();
        return user;
    }

    public void Dispose() => _fixture.Dispose();
}
