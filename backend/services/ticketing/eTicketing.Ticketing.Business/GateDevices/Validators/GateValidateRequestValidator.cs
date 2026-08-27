using eTicketing.Ticketing.Business.Tickets.Validators;
using FluentValidation;

namespace eTicketing.Ticketing.Business.GateDevices.Validators;

/// <summary>Shape-only, and deliberately the same cap as the organizer-facing
/// ValidateTicketRequestValidator — the two paths verify the same codec output, so a code that is
/// too long for one has to be too long for the other.</summary>
public class GateValidateRequestValidator : AbstractValidator<GateValidateRequest>
{
    public GateValidateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kod ulaznice je obavezan.")
            .MaximumLength(ValidateTicketRequestValidator.MaxCodeLength)
                .WithMessage($"Kod ulaznice može imati najviše {ValidateTicketRequestValidator.MaxCodeLength} znakova.");
    }
}
