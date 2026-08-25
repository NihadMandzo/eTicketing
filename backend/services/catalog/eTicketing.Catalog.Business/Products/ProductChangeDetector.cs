using eTicketing.Contracts.Events;
using eTicketing.Catalog.Data.Entities;

namespace eTicketing.Catalog.Business.Products;

/// <summary>
/// Diffs a Product against the edit about to be applied to it, and describes the difference in the
/// Bosnian labels a buyer will actually read in their inbox.
///
/// Pure and static on purpose: this is the one piece of the "tell every buyer the event moved"
/// feature with real branching in it, and keeping it free of DbContext/publisher dependencies means
/// it can be unit-tested exhaustively without a fixture.
///
/// Labels are resolved here rather than in eTicketing.Notifications because the domain meaning of a
/// field lives in the service that owns it — Notifications stays a renderer with no field-name
/// mapping table of its own.
/// </summary>
public static class ProductChangeDetector
{
    /// <summary>
    /// Compares the persisted product with the incoming request. <paramref name="oldCategoryName"/>
    /// and <paramref name="newCategoryName"/> are passed in because Product only carries CategoryId
    /// — a buyer told "Kategorija: 3 → 7" learns nothing.
    /// </summary>
    public static List<ProductFieldChange> Detect(
        Product before, UpsertProductRequest after, string oldCategoryName, string newCategoryName)
    {
        var changes = new List<ProductFieldChange>();

        Add(changes, "Naziv", before.Name, after.Name);
        Add(changes, "Opis", before.Description, after.Description);
        Add(changes, "Datum i vrijeme", FormatDate(before.Date), FormatDate(after.Date));
        Add(changes, "Grad", before.City.ToString(), after.City?.ToString());
        Add(changes, "Kategorija", oldCategoryName, newCategoryName);

        // Latitude and longitude move together and mean nothing apart, so they're reported as one
        // "the pin moved" line rather than two rows of raw decimals.
        var oldPin = FormatPin(before.Latitude, before.Longitude);
        var newPin = FormatPin(after.Latitude, after.Longitude);
        Add(changes, "Lokacija na mapi", oldPin, newPin);

        return changes;
    }

    private static void Add(List<ProductFieldChange> changes, string field, string? oldValue, string? newValue)
    {
        // Ordinal, not culture-aware: two strings that differ only by culture-specific collation
        // are still a real edit the buyer should see.
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            return;

        changes.Add(new ProductFieldChange(field, oldValue, newValue));
    }

    private static string? FormatDate(DateTime? date) => date?.ToString("dd.MM.yyyy. HH:mm");

    private static string? FormatPin(double? latitude, double? longitude) =>
        latitude is null || longitude is null ? null : $"{latitude.Value:F5}, {longitude.Value:F5}";
}
