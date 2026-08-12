using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Website).Must(BeAValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website nije validan URL.");

        // Logo is optional on update — only validated when a replacement file is provided.
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
