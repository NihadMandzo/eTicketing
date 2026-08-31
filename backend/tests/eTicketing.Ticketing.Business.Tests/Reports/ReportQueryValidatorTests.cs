using eTicketing.Ticketing.Business.Reports;
using eTicketing.Ticketing.Business.Reports.Validators;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Reports;

/// <summary>
/// The range rules are the authoritative copy — the desktop date bar mirrors them, but a
/// hand-rolled request has to be refused here. The fixture's clock is pinned to 24 August 2026,
/// which is what "today" means throughout.
/// </summary>
public class ReportQueryValidatorTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ReportQueryValidator _sut;

    public ReportQueryValidatorTests() => _sut = new ReportQueryValidator(_fixture.PlatformClock);

    private static ReportQuery Query(DateOnly from, DateOnly to) => new() { From = from, To = to };

    [Fact]
    public void Validate_ForAnOrdinaryPastRange_Passes()
    {
        var result = _sut.Validate(Query(new DateOnly(2026, 7, 26), new DateOnly(2026, 8, 24)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ForASingleDay_Passes()
    {
        var result = _sut.Validate(Query(new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 24)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenFromIsAfterTo_Fails()
    {
        var result = _sut.Validate(Query(new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Od\" mora biti prije datuma \"Do\".");
    }

    [Fact]
    public void Validate_WhenFromIsAfterTo_DoesNotAlsoComplainAboutTheRangeLength()
    {
        // An inverted range has a negative length; reporting "period is too long" on top of the
        // real problem would just be noise on the form.
        var result = _sut.Validate(Query(new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 1)));

        result.Errors.Should().NotContain(e => e.ErrorMessage.Contains("duži od"));
    }

    [Fact]
    public void Validate_WhenToIsInTheFuture_Fails()
    {
        var result = _sut.Validate(Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 25)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Do\" ne može biti u budućnosti.");
    }

    [Fact]
    public void Validate_ForExactlyTheMaximumRange_Passes()
    {
        var to = new DateOnly(2026, 8, 24);
        var result = _sut.Validate(Query(to.AddDays(-(ReportRangeValidator<ReportQuery>.MaxRangeDays - 1)), to));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OneDayBeyondTheMaximumRange_Fails()
    {
        var to = new DateOnly(2026, 8, 24);
        var result = _sut.Validate(Query(to.AddDays(-ReportRangeValidator<ReportQuery>.MaxRangeDays), to));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Period ne može biti duži od 366 dana.");
    }

    [Fact]
    public void Validate_WithMissingDates_Fails()
    {
        var result = _sut.Validate(new ReportQuery());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Od\" je obavezan.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Do\" je obavezan.");
    }

    [Fact]
    public void Validate_ExportQueryWithAnUnknownTab_Fails()
    {
        var validator = new ReportExportQueryValidator(_fixture.PlatformClock);

        var result = validator.Validate(new ReportExportQuery
        {
            Tab = (ReportTab)99,
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 24),
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Nepoznat tip izvještaja.");
    }

    [Fact]
    public void Validate_ExportQuery_AppliesTheSameRangeRules()
    {
        var validator = new ReportExportQueryValidator(_fixture.PlatformClock);

        var result = validator.Validate(new ReportExportQuery
        {
            Tab = ReportTab.Sales,
            From = new DateOnly(2026, 8, 24),
            To = new DateOnly(2026, 8, 1),
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Od\" mora biti prije datuma \"Do\".");
    }

    public void Dispose() => _fixture.Dispose();
}
