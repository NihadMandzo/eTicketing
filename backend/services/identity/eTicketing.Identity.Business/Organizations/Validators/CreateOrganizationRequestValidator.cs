using eTicketing.Identity.Data.Enums;
using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Website).Must(BeAValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website nije validan URL.");

        RuleFor(x => x.AdminFirstName).NotEmpty().Length(2, 100);
        RuleFor(x => x.AdminLastName).NotEmpty().Length(2, 100);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.AdminUsername).NotEmpty().Length(3, 50);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.AdminRole).IsInEnum()
            .Must(r => r is RoleType.OrganizationSuperAdmin or RoleType.OrganizationAdmin)
            .WithMessage("Uloga administratora organizacije mora biti OrganizationSuperAdmin ili OrganizationAdmin.");

        // Logo is optional on create — only validated when one is actually supplied.
        RuleFor(x => x.Logo)
            .Must(OrganizationLogoValidation.HasAllowedContentType)
            .WithMessage("Dozvoljeni formati loga su PNG i JPEG.")
            .When(x => x.Logo != null);
        RuleFor(x => x.Logo)
            .Must(OrganizationLogoValidation.IsWithinSizeLimit)
            .WithMessage("Logo može biti maksimalno 2MB.")
            .When(x => x.Logo != null);
        RuleFor(x => x.Logo)
            .MustAsync((file, ct) => OrganizationLogoValidation.HasValidContentAsync(file!, ct))
            .WithMessage("Logo nije validna slika ili je prevelike rezolucije.")
            .When(x => x.Logo != null);
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
}
