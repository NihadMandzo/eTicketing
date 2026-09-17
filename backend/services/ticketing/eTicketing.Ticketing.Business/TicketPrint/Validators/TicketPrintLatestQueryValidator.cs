using FluentValidation;

namespace eTicketing.Ticketing.Business.TicketPrint.Validators;

public class TicketPrintLatestQueryValidator : AbstractValidator<TicketPrintLatestQuery>
{
    public TicketPrintLatestQueryValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Proizvod je obavezan.");
    }
}
