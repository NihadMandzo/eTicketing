using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data.Enums;
using FluentAssertions;

namespace eTicketing.Identity.Business.Tests.Organizations;

public class OrganizationServiceTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IOrganizationService _sut;

    public OrganizationServiceTests()
    {
        _sut = new OrganizationService(_fixture.OrganizationRepository, _fixture.UserRepository, _fixture.UnitOfWork);
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

    public void Dispose() => _fixture.Dispose();
}
