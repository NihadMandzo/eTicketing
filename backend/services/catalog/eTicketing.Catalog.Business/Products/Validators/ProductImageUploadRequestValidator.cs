using FluentValidation;

namespace eTicketing.Catalog.Business.Products.Validators;

/// <summary>Backs POST /products/{id}/images — field-level file rules only; the "max 5 images"
/// count check lives in ProductService.UploadImageAsync (needs a DB lookup).</summary>
public class ProductImageUploadRequestValidator : AbstractValidator<ProductImageUploadRequest>
{
    public ProductImageUploadRequestValidator()
    {
        RuleFor(x => x.Image).NotNull().WithMessage("Slika proizvoda je obavezna.");
        RuleFor(x => x.Image)
            .MustAsync((file, ct) => ProductImageValidation.HasAllowedFormatAsync(file, ct))
            .WithMessage("Dozvoljeni formati slike su PNG i JPEG.")
            .When(x => x.Image != null);
        RuleFor(x => x.Image)
            .Must(ProductImageValidation.IsWithinSizeLimit)
            .WithMessage("Slika može biti maksimalno 2MB.")
            .When(x => x.Image != null);
    }
}
