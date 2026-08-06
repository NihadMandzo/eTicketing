using FluentValidation;

namespace eTicketing.Identity.Business.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 100);
        RuleFor(x => x.LastName).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.PhoneNumber).Matches(@"^\+?[0-9\s\-()]{6,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Broj telefona nije u ispravnom formatu.");
    }
}
