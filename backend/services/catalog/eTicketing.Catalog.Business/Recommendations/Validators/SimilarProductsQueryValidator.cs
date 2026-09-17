using FluentValidation;

namespace eTicketing.Catalog.Business.Recommendations.Validators;

public class SimilarProductsQueryValidator : AbstractValidator<SimilarProductsQuery>
{
    public SimilarProductsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 24).WithMessage("Broj preporuka mora biti između 1 i 24.");
    }
}
