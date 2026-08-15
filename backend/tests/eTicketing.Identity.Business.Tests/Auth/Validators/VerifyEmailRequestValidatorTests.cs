using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Auth.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Auth.Validators;

public class VerifyEmailRequestValidatorTests
{
    private readonly VerifyEmailRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_DoesNotFail()
    {
        var result = _validator.TestValidate(new VerifyEmailRequest { Code = "ABC123" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Code_Empty_Fails()
    {
        _validator.TestValidate(new VerifyEmailRequest { Code = "" })
            .ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    public void Code_WrongLength_Fails(string code)
    {
        _validator.TestValidate(new VerifyEmailRequest { Code = code })
            .ShouldHaveValidationErrorFor(x => x.Code);
    }
}
