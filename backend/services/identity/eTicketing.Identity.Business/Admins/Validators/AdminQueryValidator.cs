using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Identity.Business.Admins.Validators;

public class AdminQueryValidator : AbstractValidator<AdminQuery>
{
    public AdminQueryValidator()
    {
        Include(new BaseSearchObjectValidator<AdminQuery>());
        RuleForEach(x => x.RoleFilters).IsInEnum();
    }
}
