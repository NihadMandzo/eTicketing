using eTicketing.Identity.Business.Shared.Validators;
using FluentValidation;

namespace eTicketing.Identity.Business.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Lozinke se ne podudaraju.");
    }
}
