using FluentValidation;

namespace eTicketing.Catalog.Business.Categories.Validators;

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        // Icon is optional on update — only validated when a replacement file is provided.
        RuleFor(x => x.Icon)
            .Must(CategoryIconValidation.HasAllowedContentType)
            .WithMessage("Dozvoljeni format ikone je PNG.")
            .When(x => x.Icon != null);
        RuleFor(x => x.Icon)
            .Must(CategoryIconValidation.IsWithinSizeLimit)
            .WithMessage("Ikona je prevelika (maks. 100 KB).")
            .When(x => x.Icon != null);
        RuleFor(x => x.Icon)
            .MustAsync((file, ct) => CategoryIconValidation.HasValidContentAsync(file!, ct))
            .WithMessage("Ikona ne zadovoljava ograničenja (maks. 100x100px).")
            .When(x => x.Icon != null);
    }
}
