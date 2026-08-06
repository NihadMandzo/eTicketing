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
    public async Task GetAsync_OnlyReturnsUsersWithAdminRole()
    {
        await SeedUserAsync(RoleType.Admin, "admin1@example.com");
        await SeedUserAsync(RoleType.Admin, "admin2@example.com");
        await SeedUserAsync(RoleType.User, "buyer@example.com");
        await SeedUserAsync(RoleType.OrganizationAdmin, "orgadmin@example.com");

        var result = await _sut.GetAsync(new AdminQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(u => u.RoleName == "Admin");
    }

    [Fact]
    public async Task GetByIdAsync_ForNonAdminUser_ReturnsNotFound()
    {
        var buyer = await SeedUserAsync(RoleType.User, "buyer2@example.com");

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

    public void Dispose() => _fixture.Dispose();
}
