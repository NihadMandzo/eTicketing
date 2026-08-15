using FluentValidation;

namespace eTicketing.Identity.Business.Auth.Validators;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
