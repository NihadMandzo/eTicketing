using FluentValidation;

namespace eTicketing.Identity.Business.Admins.Validators;

public class UpdateStaffUserRequestValidator : AbstractValidator<UpdateStaffUserRequest>
{
    public UpdateStaffUserRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 100);
        RuleFor(x => x.LastName).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.PhoneNumber).Matches(@"^\+?[0-9\s\-()]{6,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Broj telefona nije u ispravnom formatu.");
    }
}
