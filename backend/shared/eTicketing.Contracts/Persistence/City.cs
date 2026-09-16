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
