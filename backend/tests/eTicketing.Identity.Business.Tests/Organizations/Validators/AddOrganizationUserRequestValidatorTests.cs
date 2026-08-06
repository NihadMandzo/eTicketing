using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Organizations.Validators;
using eTicketing.Identity.Data.Enums;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Organizations.Validators;

public class AddOrganizationUserRequestValidatorTests
{
    private readonly AddOrganizationUserRequestValidator _validator = new();

    private static AddOrganizationUserRequest ValidRequest(RoleType role) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Username = "janedoe",
        Password = "SuperSecret123",
        Role = role
    };

    [Theory]
    [InlineData(RoleType.OrganizationSuperAdmin)]
    [InlineData(RoleType.OrganizationAdmin)]
    public void Role_WithinAllowedRange_DoesNotFail(RoleType role)
    {
        var result = _validator.TestValidate(ValidRequest(role));

        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData(RoleType.SuperAdmin)]
    [InlineData(RoleType.Admin)]
    [InlineData(RoleType.User)]
    public void Role_OutOfAllowedRange_Fails(RoleType role)
    {
        var result = _validator.TestValidate(ValidRequest(role));

        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Password_TooShort_Fails()
    {
        var request = ValidRequest(RoleType.OrganizationAdmin) with { Password = "short" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Password);
    }
}
