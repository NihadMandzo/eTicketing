using FluentValidation;

namespace eTicketing.Ticketing.Business.TicketPrint.Validators;

public class TicketPrintOptionsQueryValidator : AbstractValidator<TicketPrintOptionsQuery>
{
    public TicketPrintOptionsQueryValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Proizvod je obavezan.");
    }
}
