using FluentValidation;

namespace eTicketing.Ticketing.Business.Sectors.Validators;

public class UpsertTicketTypeRequestValidator : AbstractValidator<UpsertTicketTypeRequest>
{
    public UpsertTicketTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(1, 100);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
