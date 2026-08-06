using FluentValidation;

namespace eTicketing.Identity.Business.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.EmailOrUsername).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
