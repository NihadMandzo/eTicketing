using eTicketing.Ticketing.Business.Reports.Validators;
using eTicketing.Ticketing.Business.Time;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Analytics.Validators;

/// <summary>
/// Inherits every range rule from <see cref="ReportRangeValidator{T}"/> — that generic base exists
/// so a fifth report cannot quietly grow a fifth interpretation of "From must be before To".
///
/// The one rule of its own is the horizon. It is restricted to the four values the desktop selector
/// offers rather than to a range, because the endpoint is reachable through the gateway and an
/// arbitrary horizon lets a caller ask SSA to extrapolate past its own training window — which does
/// not fail, it returns a confident-looking straight line.
///
/// The four are months rather than the days they used to be (7/14/30): an organizer plans a season,
/// not a fortnight. Asking for more projection than there is history to support it is still
/// allowed — that is a legitimate question — but it is answered on the heuristic rung with the
/// caveat printed, never as a fitted model; see <see cref="Forecasting.SsaSalesForecaster"/>.
/// </summary>
public class InsightsQueryValidator : ReportRangeValidator<InsightsQuery>
{
    /// <summary>The horizons the UI offers, and therefore the only ones the API accepts — one
    /// month, one quarter, half a year, a year. Capped at 365 for the same reason the range is:
    /// past a year the question stops being about this season's sales.</summary>
    public static readonly int[] AllowedHorizons = [30, 90, 180, 365];

    public InsightsQueryValidator(PlatformClock clock) : base(clock)
    {
        RuleFor(q => q.Horizon)
            .Must(AllowedHorizons.Contains)
            .WithMessage("Horizont prognoze mora biti 30, 90, 180 ili 365 dana.");
    }
}
