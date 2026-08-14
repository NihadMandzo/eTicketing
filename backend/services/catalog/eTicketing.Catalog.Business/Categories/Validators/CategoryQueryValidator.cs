using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Catalog.Business.Categories.Validators;

public class CategoryQueryValidator : AbstractValidator<CategoryQuery>
{
    public CategoryQueryValidator()
    {
        Include(new BaseSearchObjectValidator<CategoryQuery>());
    }
}
