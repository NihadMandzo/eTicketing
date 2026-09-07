using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

public class CancelIntentRequestValidator : AbstractValidator<CancelIntentRequest>
{
    public CancelIntentRequestValidator()
    {
        RuleFor(x => x.IntentId).NotEmpty().MaximumLength(255).WithMessage("Identifikator plaćanja je obavezan.");
    }
}

public class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(x => x.SubscriptionReference).NotEmpty().MaximumLength(255).WithMessage("Referenca pretplate je obavezna.");
    }
}
