using FluentValidation;

namespace eTicketing.Ticketing.Business.Sectors.Validators;

public class HoldSectorRequestValidator : AbstractValidator<HoldSectorRequest>
{
    public HoldSectorRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
    }
}
