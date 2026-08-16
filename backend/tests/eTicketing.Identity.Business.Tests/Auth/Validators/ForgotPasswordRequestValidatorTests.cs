using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Auth.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Auth.Validators;

public class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_DoesNotFail()
    {
        var result = _validator.TestValidate(new ForgotPasswordRequest { Email = "jane.doe@example.com" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Email_Empty_Fails()
    {
        _validator.TestValidate(new ForgotPasswordRequest { Email = "" })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_NotAValidEmail_Fails()
    {
        _validator.TestValidate(new ForgotPasswordRequest { Email = "not-an-email" })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }
}
