using FluentValidation;
using eTicketing.Ticketing.Business.Purchases;

namespace eTicketing.Ticketing.Business.Purchases.Validators;

/// <summary>Field-level rules only — cross-entity rules (hold still valid, quantities match the
/// hold, TicketTypeId belongs to the held Sector) live in PurchaseService since they need Redis/DB
/// lookups. Every rule here must be mirrored in the web Checkout step-2 form and the mobile Payment
/// screen's form, per .claude/rules/00-workflow-and-testing.md.</summary>
public class PurchaseRequestValidator : AbstractValidator<PurchaseRequest>
{
    public PurchaseRequestValidator()
    {
        RuleFor(x => x.HoldId).NotEmpty();
        RuleFor(x => x.LineItems).NotEmpty();
        RuleForEach(x => x.LineItems).ChildRules(li => li.RuleFor(x => x.Quantity).GreaterThan(0));

        // Cosmetic-only fields (see PurchaseRequest's doc comment) — still validated as real input
        // so a malformed request fails fast rather than reaching eTicketing.Payment.
        RuleFor(x => x.CardNumber).Matches(@"^\d{12,19}$").WithMessage("Broj kartice nije ispravan.");
        RuleFor(x => x.CardExpiry).Matches(@"^(0[1-9]|1[0-2])\/\d{2}$").WithMessage("Datum isteka mora biti u formatu MM/GG.");
        RuleFor(x => x.CardCvv).Matches(@"^\d{3,4}$").WithMessage("CVV nije ispravan.");
    }
}
