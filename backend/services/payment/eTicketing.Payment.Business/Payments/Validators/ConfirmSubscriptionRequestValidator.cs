using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

public class ConfirmSubscriptionRequestValidator : AbstractValidator<ConfirmSubscriptionRequest>
{
    public ConfirmSubscriptionRequestValidator()
    {
        RuleFor(x => x.SubscriptionReference).NotEmpty().MaximumLength(255).WithMessage("Referenca pretplate je obavezna.");
        RuleFor(x => x.OrderRef).NotEmpty().MaximumLength(100).WithMessage("Referenca narudžbe je obavezna.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kupac je obavezan.");
        RuleFor(x => x.ExpectedAmount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.");

        RuleFor(x => x.SimulatedLast4)
            .Matches(@"^\d{4}$")
            .When(x => !string.IsNullOrEmpty(x.SimulatedLast4))
            .WithMessage("Posljednje četiri cifre kartice nisu ispravne.");
    }
}
