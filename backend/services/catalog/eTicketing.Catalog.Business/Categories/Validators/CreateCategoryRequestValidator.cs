using FluentValidation;

namespace eTicketing.Catalog.Business.Categories.Validators;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Icon).NotNull().WithMessage("Ikona kategorije je obavezna.");
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
