using eTicketing.Ticketing.Business.Time;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Reports.Validators;

public class ReportQueryValidator : ReportRangeValidator<ReportQuery>
{
    public ReportQueryValidator(PlatformClock clock) : base(clock) { }
}
