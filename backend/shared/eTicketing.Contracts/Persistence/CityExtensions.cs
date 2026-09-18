namespace eTicketing.Contracts.Persistence;

/// <summary>
/// Turns a <see cref="City"/> into the name a person should read. The enum identifier is a C#
/// symbol, not user-facing text: it cannot carry a space or a diacritic, so <c>ToString()</c>
/// prints "BanjaLuka" and "Bihac" on printed tickets, e-mails and PDFs. Every frontend already
/// keeps its own copy of this mapping for dropdowns; this is the server-side one.
/// </summary>
public static class CityExtensions
{
    public static string ToDisplayName(this City city) => city switch
    {
        City.BanjaLuka => "Banja Luka",
        City.Bihac => "Bihać",
        City.Brcko => "Brčko",
        _ => city.ToString(),
    };
}
