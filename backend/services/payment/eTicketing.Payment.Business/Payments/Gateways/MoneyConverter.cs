namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// The single place major units (the KM figures the UI shows, stored as decimal(10,2) on
/// Payment.Amount) become the integer minor units every provider actually charges in.
/// </summary>
public static class MoneyConverter
{
    /// <summary>
    /// Both currencies this platform can be configured with, "eur" and "bam", are two-decimal, so
    /// a flat x100 is correct. Stripe does have zero-decimal currencies (JPY, KRW) and
    /// three-decimal ones (KWD, BHD) where this would be wrong by a factor of 100 or 10; if
    /// StripeOptions.Currency ever gains one of those, this needs a per-currency exponent rather
    /// than a constant.
    /// </summary>
    public static long ToMinorUnits(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal ToMajorUnits(long minorUnits) => minorUnits / 100m;
}
