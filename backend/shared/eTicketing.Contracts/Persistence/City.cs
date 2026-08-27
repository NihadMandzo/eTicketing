namespace eTicketing.Contracts.Persistence;

/// <summary>
/// Fixed set of Bosnia and Herzegovina cities a Product can be located in — drives the
/// server-side location filter on GET /products* and the city picker shown when an organizer
/// creates a Product. Declaration order is the wire contract: every frontend (web, desktop,
/// mobile) mirrors this exact order in its own enum, since the value crosses the wire as an
/// ordinal. Extensible later by appending new values — never reorder or remove existing ones.
/// </summary>
public enum City
{
    Sarajevo,
    Mostar,
    BanjaLuka,
    Tuzla,
    Zenica,
    Bihac,
    Brcko,
    Trebinje
}

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
