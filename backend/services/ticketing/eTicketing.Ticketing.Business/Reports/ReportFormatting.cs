using System.Globalization;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// Bosnian number/date formatting for the exported PDF.
///
/// The PDF is rendered on the server, so it cannot lean on the clients' formatting the way the
/// screen does — and it must not diverge from it either. These helpers mirror
/// frontend/desktop/lib/core/formatting.dart: "1.284.600 KM" (dot thousands separator, comma
/// decimal), "12,4%", "23. jul 2026.".
///
/// The culture is pinned explicitly rather than taken from the request or the host, so an export
/// produces identical output on a developer's machine and in the container.
/// </summary>
internal static class ReportFormatting
{
    /// <summary>bs-Latn-BA is not guaranteed to be present in a slim container image; de-DE has
    /// exactly the separators Bosnian uses (. for thousands, , for decimals) and ships with ICU
    /// everywhere, so it is used as the numeric culture and the month names are supplied below.</summary>
    private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Full Bosnian month names, nominative — the same table the desktop app formats
    /// dates with.</summary>
    private static readonly string[] Months =
    [
        "januar", "februar", "mart", "april", "maj", "juni",
        "juli", "avgust", "septembar", "oktobar", "novembar", "decembar"
    ];

    /// <summary>Money, always with two decimals and the KM suffix: "1.284.600,00 KM".</summary>
    public static string Money(decimal value) => value.ToString("N2", Numbers) + " KM";

    /// <summary>A plain count: "8.942".</summary>
    public static string Count(int value) => value.ToString("N0", Numbers);

    /// <summary>A percentage with one decimal: "12,4%". Null renders as an em dash, which is what
    /// every "we cannot compute this" case in these reports shows.</summary>
    public static string Percent(decimal? value) =>
        value is null ? "—" : value.Value.ToString("N1", Numbers) + "%";

    /// <summary>A signed percentage for period-over-period change: "+18,2%" / "−4,1%". Uses the
    /// typographic minus U+2212 rather than a hyphen, matching the design.</summary>
    public static string SignedPercent(decimal? value)
    {
        if (value is null) return "—";
        var formatted = Math.Abs(value.Value).ToString("N1", Numbers) + "%";
        return value.Value < 0 ? "−" + formatted : "+" + formatted;
    }

    /// <summary>"23. juli 2026."</summary>
    public static string Date(DateOnly value) => $"{value.Day}. {Months[value.Month - 1]} {value.Year}.";

    /// <summary>The header's period line: "23. juli 2026. – 21. avgust 2026.".</summary>
    public static string Range(DateOnly from, DateOnly to) => $"{Date(from)} – {Date(to)}";

    /// <summary>An hour window, as the design's "19:00 – 20:00" tile.</summary>
    public static string HourWindow(int? hour) =>
        hour is null ? "—" : $"{hour:00}:00 – {(hour + 1) % 24:00}:00";
}
