using FluentValidation;

namespace eTicketing.Ticketing.Business.GateDevices.Validators;

/// <summary>Field-level rules only. Whether a sector actually belongs to the product, and whether
/// the caller owns that product, are cross-entity rules that need the DB and Catalog — they live in
/// GateDeviceService.ValidateScopeAsync. Reused for create and update (same request shape).</summary>
public class UpsertGateDeviceRequestValidator : AbstractValidator<UpsertGateDeviceRequest>
{
    /// <summary>A venue with more than this many sectors behind one door is a data-entry mistake,
    /// not a real gate. Caps how much work one request can push onto the sector-ownership query.</summary>
    public const int MaxSectors = 50;

    public const int MaxNameLength = 100;

    public UpsertGateDeviceRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Proizvod je obavezan.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Naziv uređaja je obavezan.")
            .MaximumLength(MaxNameLength).WithMessage($"Naziv može imati najviše {MaxNameLength} znakova.");

        // Only enforced when AllSectors is off — an all-sectors gate legitimately sends none.
        When(x => !x.AllSectors, () =>
        {
            RuleFor(x => x.SectorIds)
                .NotEmpty().WithMessage("Odaberite najmanje jedan sektor ili uključite opciju 'Svi sektori'.")
                .Must(ids => ids.Count <= MaxSectors)
                    .WithMessage($"Uređaj može pokrivati najviše {MaxSectors} sektora.")
                .Must(ids => ids.All(id => id != Guid.Empty))
                    .WithMessage("Lista sektora sadrži neispravan identifikator.")
                .Must(ids => ids.Distinct().Count() == ids.Count)
                    .WithMessage("Isti sektor je odabran više puta.");
        });
    }
}
