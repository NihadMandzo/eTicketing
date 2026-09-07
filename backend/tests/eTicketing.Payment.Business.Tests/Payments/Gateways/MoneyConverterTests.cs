using eTicketing.Payment.Business.Payments.Gateways;
using FluentAssertions;

namespace eTicketing.Payment.Business.Tests.Payments.Gateways;

/// <summary>Getting this wrong charges someone 100x, so it is worth its own tests.</summary>
public class MoneyConverterTests
{
    [Theory]
    [InlineData(0.01, 1)]
    [InlineData(1, 100)]
    [InlineData(12.34, 1234)]
    [InlineData(99999999.99, 9999999999)]
    public void ToMinorUnits_ConvertsMajorUnitsExactly(decimal major, long expected)
        => MoneyConverter.ToMinorUnits(major).Should().Be(expected);

    /// <summary>Away-from-zero, not banker's rounding: a half-fenning difference must never round
    /// down in the platform's favour, and .NET's default MidpointRounding would do exactly that.</summary>
    [Theory]
    [InlineData(0.005, 1)]
    [InlineData(0.015, 2)]
    public void ToMinorUnits_RoundsHalvesAwayFromZero(decimal major, long expected)
        => MoneyConverter.ToMinorUnits(major).Should().Be(expected);

    [Fact]
    public void ToMajorUnits_IsTheInverse()
        => MoneyConverter.ToMajorUnits(1234).Should().Be(12.34m);
}
