using eTicketing.Identity.Business.Shared.Validators;
using FluentValidation;

namespace eTicketing.Identity.Business.Admins.Validators;

public class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).Password();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Lozinke se ne podudaraju.");
    }
}
