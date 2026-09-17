using eTicketing.Ticketing.Business.Time;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Reports.Validators;

/// <summary>
/// The one authoritative copy of the range rules (.claude/rules/10-backend.md) — the desktop
/// screen mirrors all three in its own date bar, but they are enforced here regardless.
///
/// Generic over <see cref="IReportRange"/> so the four data endpoints and the export endpoint
/// share one implementation instead of two copies that drift apart.
///
/// The 366-day cap is not arbitrary: ReportService buckets the daily rows in memory, so the cap is
/// also the bound on how many rows that loop can ever see, and a leap year still fits.
/// </summary>
public abstract class ReportRangeValidator<T> : AbstractValidator<T> where T : IReportRange
{
    /// <summary>Longest range a single report may cover, inclusive of both endpoints.</summary>
    public const int MaxRangeDays = 366;

    protected ReportRangeValidator(PlatformClock clock)
    {
        RuleFor(q => q.From)
            .NotEqual(default(DateOnly))
            .WithMessage("Datum \"Od\" je obavezan.");

        RuleFor(q => q.To)
            .NotEqual(default(DateOnly))
            .WithMessage("Datum \"Do\" je obavezan.");

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("Datum \"Od\" mora biti prije datuma \"Do\".");

        // "Today" comes from PlatformClock, not UtcNow: an organizer in Sarajevo picking today's
        // date at 00:30 local is picking a date UTC has not reached yet, and a UtcNow comparison
        // would reject a perfectly ordinary request for two hours every night.
        RuleFor(q => q.To)
            .LessThanOrEqualTo(_ => clock.Today())
            .WithMessage("Datum \"Do\" ne može biti u budućnosti.");

        // Guarded on From <= To so an inverted range reports only the "Od mora biti prije Do"
        // message above, instead of also complaining about a negative length nobody asked for.
        RuleFor(q => q.To)
            .Must((q, to) => q.From > to || to.DayNumber - q.From.DayNumber + 1 <= MaxRangeDays)
            .WithMessage($"Period ne može biti duži od {MaxRangeDays} dana.");
    }
}
