using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Identity.Business.Organizations.Validators;

public class OrganizationQueryValidator : AbstractValidator<OrganizationQuery>
{
    public OrganizationQueryValidator()
    {
        Include(new BaseSearchObjectValidator<OrganizationQuery>());
    }
}
