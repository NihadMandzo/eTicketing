using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

/// <summary>Shared between POST and PUT /organizations/{id}/logo — both accept the exact same
/// shape, so one validator covers create and replace.</summary>
public class OrganizationLogoUploadRequestValidator : AbstractValidator<OrganizationLogoUploadRequest>
{
    public OrganizationLogoUploadRequestValidator()
    {
        RuleFor(x => x.Logo).NotNull().WithMessage("Logo organizacije je obavezan.");
        RuleFor(x => x.Logo)
            .MustAsync((file, ct) => OrganizationLogoValidation.HasAllowedFormatAsync(file, ct))
            .WithMessage("Dozvoljeni formati loga su PNG i JPEG.")
            .When(x => x.Logo != null);
        RuleFor(x => x.Logo)
            .Must(OrganizationLogoValidation.IsWithinSizeLimit)
            .WithMessage("Logo može biti maksimalno 1MB.")
            .When(x => x.Logo != null);
        RuleFor(x => x.Logo)
            .MustAsync((file, ct) => OrganizationLogoValidation.HasSquareAspectRatioAsync(file, ct))
            .WithMessage("Logo mora biti kvadratan (omjer 1:1).")
            .When(x => x.Logo != null);
    }
}
