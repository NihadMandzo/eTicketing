using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Admins.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Admins.Validators;

public class UpdateStaffUserRequestValidatorTests
{
    private readonly UpdateStaffUserRequestValidator _validator = new();

    private static UpdateStaffUserRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Username = "janedoe",
        PhoneNumber = "+387 61 111 222"
    };

    [Fact]
    public void ValidRequest_DoesNotFail()
    {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Email_Empty_Fails()
    {
        var request = ValidRequest() with { Email = "" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_NotAValidEmail_Fails()
    {
        var request = ValidRequest() with { Email = "not-an-email" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Username_TooShort_Fails()
    {
        var request = ValidRequest() with { Username = "ab" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void FirstName_Empty_Fails()
    {
        var request = ValidRequest() with { FirstName = "" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void PhoneNumber_Null_DoesNotFail()
    {
        var request = ValidRequest() with { PhoneNumber = null };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void PhoneNumber_InvalidFormat_Fails()
    {
        var request = ValidRequest() with { PhoneNumber = "not-a-phone-number" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }
}
