using eTicketing.Ticketing.Business.Time;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Reports.Validators;

public class ReportExportQueryValidator : ReportRangeValidator<ReportExportQuery>
{
    public ReportExportQueryValidator(PlatformClock clock) : base(clock)
    {
        RuleFor(q => q.Tab)
            .IsInEnum()
            .WithMessage("Nepoznat tip izvještaja.");
    }
}
