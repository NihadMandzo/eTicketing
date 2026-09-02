using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Admins.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Admins.Validators;

public class SetPasswordRequestValidatorTests
{
    private readonly SetPasswordRequestValidator _validator = new();

    private static SetPasswordRequest ValidRequest() => new()
    {
        NewPassword = "BrandNewPassword123!",
        ConfirmPassword = "BrandNewPassword123!"
    };

    [Fact]
    public void ValidRequest_DoesNotFail()
    {
        _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NewPassword_TooShort_Fails()
    {
        _validator.TestValidate(ValidRequest() with { NewPassword = "short", ConfirmPassword = "short" })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void ConfirmPassword_NotMatching_Fails()
    {
        _validator.TestValidate(ValidRequest() with { ConfirmPassword = "SomethingElse123!" })
            .ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }
}
