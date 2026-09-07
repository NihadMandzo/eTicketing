using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

/// <summary>
/// Field-shape rules only. The genuinely important checks -- that the amount matches the held
/// sector's real price, and that the intent belongs to this buyer -- cannot live here: the first
/// needs Redis and the catalog and is enforced by Ticketing's PurchaseService, the second needs the
/// provider and is enforced in StripePaymentGateway.CaptureAsync.
/// </summary>
public class CreateIntentRequestValidator : AbstractValidator<CreateIntentRequest>
{
    public CreateIntentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.");
        RuleFor(x => x.OrderRef).NotEmpty().MaximumLength(100).WithMessage("Referenca narudžbe je obavezna.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kupac je obavezan.");
        RuleFor(x => x.HoldRef).NotEmpty().WithMessage("Referenca rezervacije je obavezna.");
        RuleFor(x => x.SectorId).NotEmpty().WithMessage("Sektor je obavezan.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300).WithMessage("Opis plaćanja je obavezan.");

        RuleFor(x => x.CustomerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("Email kupca nije ispravan.");

        // A subscription bills a named product every month; the name reaches the buyer's card
        // statement, so it cannot be blank.
        RuleFor(x => x.SubscriptionProductName)
            .NotEmpty()
            .MaximumLength(250)
            .When(x => x.IsSubscription)
            .WithMessage("Naziv pretplate je obavezan.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .When(x => x.IsSubscription)
            .WithMessage("Email kupca je obavezan za pretplatu.");
    }
}
