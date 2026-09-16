using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

public class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(x => x.SubscriptionReference).NotEmpty().MaximumLength(255).WithMessage("Referenca pretplate je obavezna.");
    }
}
