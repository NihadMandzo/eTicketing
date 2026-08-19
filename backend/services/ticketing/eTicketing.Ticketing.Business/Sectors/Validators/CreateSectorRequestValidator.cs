using FluentValidation;

namespace eTicketing.Ticketing.Business.Sectors.Validators;

/// <summary>Field-level rules only — the cross-entity rules that need the owning Product's data
/// (product must exist/be owned by caller, Period/Capacity fields matching TicketingMode) live in
/// SectorService.ValidateAsync so preview and create/update always agree on the same message.
/// Reused for both create and update (same request shape, see UpsertSectorRequest).</summary>
public class CreateSectorRequestValidator : AbstractValidator<UpsertSectorRequest>
{
    public CreateSectorRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Length(1, 200);
        RuleFor(x => x.Capacity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PeriodMonth).InclusiveBetween(1, 12).When(x => x.PeriodMonth is not null);
    }
}
