using FluentValidation;

namespace eTicketing.Catalog.Business.Recommendations.Validators;

/// <summary>Take is capped because every recommendation surface is a single row of cards — a
/// caller asking for 500 is either confused or probing, and either way the candidate pool would be
/// ranked for nothing.</summary>
public class RecommendationQueryValidator : AbstractValidator<RecommendationQuery>
{
    public RecommendationQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 24).WithMessage("Broj preporuka mora biti između 1 i 24.");
    }
}

public class SimilarProductsQueryValidator : AbstractValidator<SimilarProductsQuery>
{
    public SimilarProductsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 24).WithMessage("Broj preporuka mora biti između 1 i 24.");
    }
}

public class PopularProductsQueryValidator : AbstractValidator<PopularProductsQuery>
{
    public PopularProductsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 24).WithMessage("Broj preporuka mora biti između 1 i 24.");
        RuleFor(x => x.City).IsInEnum().When(x => x.City.HasValue).WithMessage("Nepoznat grad.");
    }
}
