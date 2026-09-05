using FluentValidation;

namespace eTicketing.Ticketing.Business.Tickets.Validators;

/// <summary>Shape-only checks — whether the code corresponds to a real, unused, right-product
/// ticket is TicketValidationService's job, and deliberately not a validation failure when it
/// doesn't (see TicketValidationResponse). This validator only keeps obviously-empty and
/// absurdly-long input from reaching the HMAC verifier and the database.</summary>
public class ValidateTicketRequestValidator : AbstractValidator<ValidateTicketRequest>
{
    /// <summary>A signed payload is ~60 characters; 200 leaves generous headroom while capping
    /// what a rogue client can push through the codec.</summary>
    public const int MaxCodeLength = 200;

    /// <summary>Matches UpsertGateDeviceRequestValidator.MaxSectors — the two describe the same
    /// thing (how many sectors one door can cover) and must not drift apart.</summary>
    public const int MaxSectors = 50;

    public ValidateTicketRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Događaj je obavezan.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kod ulaznice je obavezan.")
            .MaximumLength(MaxCodeLength).WithMessage($"Kod ulaznice može imati najviše {MaxCodeLength} znakova.");

        // Optional field — only checked when the caller actually narrowed to sectors.
        When(x => x.SectorIds is { Count: > 0 }, () =>
        {
            RuleFor(x => x.SectorIds!)
                .Must(ids => ids.Count <= MaxSectors)
                    .WithMessage($"Moguće je odabrati najviše {MaxSectors} sektora.")
                .Must(ids => ids.All(id => id != Guid.Empty))
                    .WithMessage("Lista sektora sadrži neispravan identifikator.");
        });
    }
}
