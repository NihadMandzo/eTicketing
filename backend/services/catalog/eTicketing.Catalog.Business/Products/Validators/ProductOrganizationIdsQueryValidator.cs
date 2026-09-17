using FluentValidation;

namespace eTicketing.Catalog.Business.Products.Validators;

public class ProductOrganizationIdsQueryValidator : AbstractValidator<ProductOrganizationIdsQuery>
{
    public ProductOrganizationIdsQueryValidator()
    {
        // Previously an unparseable token was quietly dropped, so "?categoryIds=1,abc" filtered by
        // category 1 and the caller never learned the rest of its filter had been ignored. Both rules
        // read ProductOrganizationIdsQuery.Tokens() rather than splitting on their own, so what is
        // validated is exactly what gets parsed — including the empty tokens in "1,,2" or ",".
        RuleFor(x => x.CategoryIds)
            .Must((query, _) => query.Tokens().All(token => int.TryParse(token, out var id) && id > 0))
            .When(x => !string.IsNullOrWhiteSpace(x.CategoryIds))
            .WithMessage("Lista kategorija smije sadržavati samo pozitivne cijele brojeve odvojene zarezom.");

        RuleFor(x => x.CategoryIds)
            .Must((query, _) => query.Tokens().Count <= ProductOrganizationIdsQuery.MaxCategoryIds)
            .When(x => !string.IsNullOrWhiteSpace(x.CategoryIds))
            .WithMessage($"Najviše {ProductOrganizationIdsQuery.MaxCategoryIds} kategorija po zahtjevu.");
    }
}
