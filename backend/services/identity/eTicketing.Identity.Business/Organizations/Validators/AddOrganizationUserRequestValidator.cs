using eTicketing.Identity.Data.Enums;
using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

public class AddOrganizationUserRequestValidator : AbstractValidator<AddOrganizationUserRequest>
{
    public AddOrganizationUserRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 100);
        RuleFor(x => x.LastName).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum()
            .Must(r => r is RoleType.OrganizationSuperAdmin or RoleType.OrganizationAdmin)
            .WithMessage("Uloga mora biti OrganizationSuperAdmin ili OrganizationAdmin.");
    }
}
