using FluentValidation;

namespace eTicketing.Ticketing.Business.Purchases.Validators;

/// <summary>Field-level rules only — cross-entity rules (hold still valid, quantities match the
/// hold, TicketTypeId belongs to the held Sector) live in PurchaseService since they need Redis/DB
/// lookups. Every rule here must be mirrored in the web Checkout form and the mobile Payment
/// screen's form, per .claude/rules/00-workflow-and-testing.md.
///
/// There are no card rules any more: with a real payment provider the card never reaches this
/// service. The one card-shaped field left is SimulatedLast4, which only the Mock provider reads,
/// and it is optional because the Stripe provider never sends it.</summary>
public class PurchaseRequestValidator : AbstractValidator<PurchaseRequest>
{
    public PurchaseRequestValidator()
    {
        RuleFor(x => x.HoldId).NotEmpty();
        RuleFor(x => x.LineItems).NotEmpty();
        RuleForEach(x => x.LineItems).ChildRules(li => li.RuleFor(x => x.Quantity).GreaterThan(0));

        RuleFor(x => x.LineItems)
            .Must(items => items.Select(li => li.TicketTypeId is null).Distinct().Count() <= 1)
            .WithMessage("Svaka stavka narudžbe mora ili imati tip ulaznice ili ga sve moraju izostaviti.");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Nedostaje identifikator narudžbe. Osvježite stranicu i pokušajte ponovo.");

        RuleFor(x => x.PaymentIntentId)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Nedostaje potvrda plaćanja. Osvježite stranicu i pokušajte ponovo.");

        RuleFor(x => x.SimulatedLast4)
            .Matches(@"^\d{4}$")
            .When(x => !string.IsNullOrEmpty(x.SimulatedLast4))
            .WithMessage("Broj kartice nije ispravan.");
    }
}
