using eTicketing.Ticketing.Business.Reports.Validators;
using eTicketing.Ticketing.Business.Time;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Analytics.Validators;

/// <summary>
/// Inherits every range rule from <see cref="ReportRangeValidator{T}"/> — that generic base exists
/// so a fifth report cannot quietly grow a fifth interpretation of "From must be before To".
///
/// The one rule of its own is the horizon. It is restricted to the three values the desktop
/// selector offers rather than to a range, because the endpoint is reachable through the gateway
/// and an arbitrary horizon lets a caller ask SSA to extrapolate a year past its own training
/// window — which does not fail, it returns a confident-looking straight line.
/// </summary>
public class InsightsQueryValidator : ReportRangeValidator<InsightsQuery>
{
    /// <summary>The horizons the UI offers, and therefore the only ones the API accepts.</summary>
    public static readonly int[] AllowedHorizons = [7, 14, 30];

    public InsightsQueryValidator(PlatformClock clock) : base(clock)
    {
        RuleFor(q => q.Horizon)
            .Must(AllowedHorizons.Contains)
            .WithMessage("Horizont prognoze mora biti 7, 14 ili 30 dana.");
    }
}
