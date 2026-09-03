using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Validators;
using eTicketing.Ticketing.Business.Reports.Validators;
using eTicketing.Ticketing.Business.Time;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The horizon rule, plus proof that the inherited range rules still run — the whole point of
/// InsightsQueryValidator extending ReportRangeValidator is that a fifth report cannot quietly grow
/// a fifth interpretation of "From must be before To".
/// </summary>
public class InsightsQueryValidatorTests
{
    private readonly InsightsQueryValidator _sut;

    /// <summary>Pinned to 24 August 2026, matching the fixture the rest of the suite uses, so
    /// "not in the future" means the same thing here as everywhere else.</summary>
    private static readonly DateOnly Today = new(2026, 8, 24);

    public InsightsQueryValidatorTests()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
        var clock = new PlatformClock(timeProvider, MsOptions.Create(new PlatformTimeOptions { TimeZoneId = "Europe/Sarajevo" }));
        _sut = new InsightsQueryValidator(clock);
    }

    private static InsightsQuery Query(int horizon = 30, DateOnly? from = null, DateOnly? to = null) => new()
    {
        From = from ?? Today.AddDays(-30),
        To = to ?? Today,
        Horizon = horizon,
    };

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(365)]
    public void Validate_ForAnOfferedHorizon_Passes(int horizon)
    {
        _sut.Validate(Query(horizon)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-7)]
    // The three the selector used to offer: a stale client must be refused, not quietly served a
    // horizon the screen can no longer name.
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(31)]
    [InlineData(400)]
    public void Validate_ForAHorizonTheUiNeverOffers_FailsWithTheBosnianMessage(int horizon)
    {
        var result = _sut.Validate(Query(horizon));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Horizont prognoze mora biti 30, 90, 180 ili 365 dana.");
    }

    [Fact]
    public void Validate_WithNoHorizonGiven_DefaultsToAValidOne()
    {
        // The query binds from the query string; an absent horizon must not be a 400.
        var result = _sut.Validate(new InsightsQuery { From = Today.AddDays(-30), To = Today });

        result.IsValid.Should().BeTrue();
    }

    // ── Inherited range rules ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ForAnInvertedRange_Fails()
    {
        var result = _sut.Validate(Query(from: Today, to: Today.AddDays(-10)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Od\" mora biti prije datuma \"Do\".");
    }

    [Fact]
    public void Validate_ForARangeEndingInTheFuture_Fails()
    {
        var result = _sut.Validate(Query(to: Today.AddDays(1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Do\" ne može biti u budućnosti.");
    }

    [Fact]
    public void Validate_AtTheMaximumRangeLength_Passes()
    {
        var result = _sut.Validate(Query(from: Today.AddDays(-(ReportRangeValidator<InsightsQuery>.MaxRangeDays - 1))));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_JustPastTheMaximumRangeLength_Fails()
    {
        var result = _sut.Validate(Query(from: Today.AddDays(-ReportRangeValidator<InsightsQuery>.MaxRangeDays)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("366"));
    }

    [Fact]
    public void Validate_WithNoDatesAtAll_ReportsBothAsRequired()
    {
        var result = _sut.Validate(new InsightsQuery());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Od\" je obavezan.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Datum \"Do\" je obavezan.");
    }
}
