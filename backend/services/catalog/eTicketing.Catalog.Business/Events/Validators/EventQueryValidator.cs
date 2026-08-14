using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Catalog.Business.Events.Validators;

public class EventQueryValidator : AbstractValidator<EventQuery>
{
    public EventQueryValidator()
    {
        Include(new BaseSearchObjectValidator<EventQuery>());
    }
}
