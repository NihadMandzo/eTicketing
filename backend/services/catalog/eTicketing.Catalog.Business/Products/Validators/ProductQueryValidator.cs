using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Catalog.Business.Products.Validators;

public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        Include(new BaseSearchObjectValidator<ProductQuery>());

        // Optional filter — only validated when present, unlike the required City on create/update.
        RuleFor(x => x.City).IsInEnum().When(x => x.City.HasValue);
    }
}
