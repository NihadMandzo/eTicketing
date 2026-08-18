using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Catalog.Business.Products.Validators;

public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        Include(new BaseSearchObjectValidator<ProductQuery>());
    }
}
