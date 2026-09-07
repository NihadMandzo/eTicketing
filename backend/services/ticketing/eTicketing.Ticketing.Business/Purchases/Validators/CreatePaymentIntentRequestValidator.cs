using FluentValidation;

namespace eTicketing.Ticketing.Business.Purchases.Validators;

/// <summary>Mirrors PurchaseRequestValidator's reservation rules, minus the payment fields that do
/// not exist yet at this point in the flow. Both must be mirrored in the web Checkout form and the
/// mobile Payment screen, per .claude/rules/00-workflow-and-testing.md.</summary>
public class CreatePaymentIntentRequestValidator : AbstractValidator<CreatePaymentIntentRequest>
{
    public CreatePaymentIntentRequestValidator()
    {
        RuleFor(x => x.HoldId).NotEmpty();
        RuleFor(x => x.LineItems).NotEmpty();
        RuleForEach(x => x.LineItems).ChildRules(li => li.RuleFor(x => x.Quantity).GreaterThan(0));

        RuleFor(x => x.LineItems)
            .Must(items => items.Select(li => li.TicketTypeId is null).Distinct().Count() <= 1)
            .WithMessage("Svaka stavka narudžbe mora ili imati tip ulaznice ili ga sve moraju izostaviti.");
    }
}
