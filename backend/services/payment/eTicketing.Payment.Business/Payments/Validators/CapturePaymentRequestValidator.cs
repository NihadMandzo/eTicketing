using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

public class CapturePaymentRequestValidator : AbstractValidator<CapturePaymentRequest>
{
    public CapturePaymentRequestValidator()
    {
        RuleFor(x => x.IntentId).NotEmpty().MaximumLength(255).WithMessage("Identifikator plaćanja je obavezan.");
        RuleFor(x => x.OrderRef).NotEmpty().MaximumLength(100).WithMessage("Referenca narudžbe je obavezna.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kupac je obavezan.");
        RuleFor(x => x.ExpectedAmount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.");

        // Mock provider only, and optional even then -- the Stripe provider never sends it. The
        // pattern still runs when a value IS present so a malformed one fails fast.
        RuleFor(x => x.SimulatedLast4)
            .Matches(@"^\d{4}$")
            .When(x => !string.IsNullOrEmpty(x.SimulatedLast4))
            .WithMessage("Posljednje četiri cifre kartice nisu ispravne.");
    }
}
