using FluentValidation;

namespace eTicketing.Catalog.Business.Recommendations.Validators;

public class PopularProductsQueryValidator : AbstractValidator<PopularProductsQuery>
{
    public PopularProductsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 24).WithMessage("Broj preporuka mora biti između 1 i 24.");
        RuleFor(x => x.City).IsInEnum().When(x => x.City.HasValue).WithMessage("Nepoznat grad.");
    }
}
