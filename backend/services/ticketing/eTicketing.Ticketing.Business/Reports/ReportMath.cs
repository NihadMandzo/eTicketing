namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Arithmetic shared by every report and by the analytics blocks, so a percentage means
/// the same thing and is rounded the same way wherever it is computed.</summary>
internal static class ReportMath
{
    /// <summary>Division that answers 0 instead of throwing on an empty period. Every ratio in
    /// these reports has a legitimately-zero denominator (a range with no sales, a product with no
    /// check-ins), so guarding at each call site would be noise.</summary>
    public static decimal Divide(decimal numerator, decimal denominator)
        => denominator == 0 ? 0m : numerator / denominator;

    /// <summary>Percentages and averages are rounded once, here, so the clients can render what
    /// they are given rather than each rounding a long decimal their own way.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
