using FluentValidation;

namespace eTicketing.Ticketing.Business.TicketPrint.Validators;

/// <summary>
/// Shape and size rules for a print request. Ownership, publication status, price-tier matching and
/// remaining capacity all need the database and live in <see cref="TicketPrintService"/> instead —
/// this validator is the cheap gate that runs first.
///
/// Every rule here is mirrored in the desktop export screen's own form checks, per
/// .claude/rules/00-workflow-and-testing.md. This copy remains the authoritative one.
/// </summary>
public class CreateTicketPrintBatchRequestValidator : AbstractValidator<CreateTicketPrintBatchRequest>
{
    public CreateTicketPrintBatchRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Proizvod je obavezan.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Odaberite barem jedan sektor i broj ulaznica.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SectorId)
                .NotEmpty().WithMessage("Sektor je obavezan.");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Broj ulaznica mora biti veći od 0.")
                .LessThanOrEqualTo(TicketPrintService.MaxTicketsPerBatch)
                .WithMessage($"Broj ulaznica po sektoru ne može biti veći od {TicketPrintService.MaxTicketsPerBatch}.");
        });

        RuleFor(x => x.Lines)
            .Must(lines => lines.Sum(l => (long)l.Quantity) <= TicketPrintService.MaxTicketsPerBatch)
            .WithMessage($"Jedan izvoz može sadržavati najviše {TicketPrintService.MaxTicketsPerBatch} ulaznica.")
            .When(x => x.Lines.Count > 0);

        // Two lines for the same (sector, ticket type) would silently double an organizer's intent
        // — reject it rather than guess whether they meant the sum or the larger of the two.
        RuleFor(x => x.Lines)
            .Must(lines => lines
                .Select(l => (l.SectorId, l.TicketTypeId))
                .Distinct()
                .Count() == lines.Count)
            .WithMessage("Isti sektor i vrsta ulaznice se ne mogu navesti dva puta.")
            .When(x => x.Lines.Count > 0);
    }
}
