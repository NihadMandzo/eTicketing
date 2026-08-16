using FluentValidation;

namespace eTicketing.Identity.Business.Auth.Validators;

public class SetNewPasswordRequestValidator : AbstractValidator<SetNewPasswordRequest>
{
    public SetNewPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Lozinke se ne podudaraju.");
    }
}
