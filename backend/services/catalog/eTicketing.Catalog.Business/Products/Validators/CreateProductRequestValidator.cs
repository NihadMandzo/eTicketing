using FluentValidation;

namespace eTicketing.Catalog.Business.Products.Validators;

/// <summary>Field-level rules only — the cross-entity rules that need a DB lookup (category must
/// exist, Date required-iff-SingleOccurrence) live in ProductService.ValidateAsync so preview and
/// create/update always agree on the same message. Reused for both create and update (same
/// request shape, see UpsertProductRequest) — registered once, FluentValidation picks it up for
/// both endpoints since both bind the same UpsertProductRequest type.</summary>
public class CreateProductRequestValidator : AbstractValidator<UpsertProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.CategoryId).GreaterThan(0);

        // The organizer must place an exact map pin when creating/editing a product — required,
        // same tier as Name/CategoryId, not merely validated-when-present.
        RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90, 90).WithMessage("Lokacija je obavezna — postavite tačku na mapi.");
        RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180, 180).WithMessage("Lokacija je obavezna — postavite tačku na mapi.");
        RuleFor(x => x.City).NotNull().IsInEnum().WithMessage("Grad je obavezan.");
    }
}
