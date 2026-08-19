using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Sectors.Validators;

public class SectorQueryValidator : AbstractValidator<SectorQuery>
{
    public SectorQueryValidator()
    {
        Include(new BaseSearchObjectValidator<SectorQuery>());
    }
}
