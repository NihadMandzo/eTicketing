using FluentValidation;

namespace eTicketing.Catalog.Business.Categories.Validators;

/// <summary>Shared between POST and PUT /categories/{id}/icon — both accept the exact same
/// shape, so one validator covers create and replace.</summary>
public class CategoryIconUploadRequestValidator : AbstractValidator<CategoryIconUploadRequest>
{
    public CategoryIconUploadRequestValidator()
    {
        RuleFor(x => x.Icon).NotNull().WithMessage("Ikona kategorije je obavezna.");
        RuleFor(x => x.Icon)
            .MustAsync((file, ct) => CategoryIconValidation.HasAllowedFormatAsync(file, ct))
            .WithMessage("Dozvoljeni format ikone je PNG.")
            .When(x => x.Icon != null);
        RuleFor(x => x.Icon)
            .Must(CategoryIconValidation.IsWithinSizeLimit)
            .WithMessage("Ikona može biti maksimalno 1MB.")
            .When(x => x.Icon != null);
        RuleFor(x => x.Icon)
            .MustAsync((file, ct) => CategoryIconValidation.HasSquareAspectRatioAsync(file!, ct))
            .WithMessage("Ikona mora biti kvadratna (omjer 1:1).")
            .When(x => x.Icon != null);
    }
}
