using FluentValidation;

namespace eTicketing.Catalog.Business.Recommendations.Validators;

/// <summary>Field-level rules only — that the product exists and is Published needs a DB lookup
/// and lives in RecommendationService.TrackViewAsync, same split as
/// CreateProductRequestValidator/ProductService.ValidateAsync.</summary>
public class TrackViewRequestValidator : AbstractValidator<TrackViewRequest>
{
    public TrackViewRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Proizvod je obavezan.");
    }
}
