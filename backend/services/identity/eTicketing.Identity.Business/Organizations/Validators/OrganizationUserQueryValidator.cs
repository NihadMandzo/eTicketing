using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

public class OrganizationUserQueryValidator : AbstractValidator<OrganizationUserQuery>
{
    public OrganizationUserQueryValidator()
    {
        Include(new BaseSearchObjectValidator<OrganizationUserQuery>());
    }
}
