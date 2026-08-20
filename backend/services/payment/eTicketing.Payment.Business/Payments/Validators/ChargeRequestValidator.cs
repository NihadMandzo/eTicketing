using FluentValidation;

namespace eTicketing.Payment.Business.Payments.Validators;

public class ChargeRequestValidator : AbstractValidator<ChargeRequest>
{
    public ChargeRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.OrderRef).NotEmpty();
        RuleFor(x => x.CardNumberLast4).Matches(@"^\d{4}$");
    }
}
