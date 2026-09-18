namespace eTicketing.Contracts.Events;

/// <summary><paramref name="Field"/> is the Bosnian display label ("Datum i vrijeme", "Grad", ...),
/// resolved in Catalog where the domain meaning lives, so eTicketing.Notifications stays a dumb
/// renderer with no field-name mapping table of its own.</summary>
public record ProductFieldChange(string Field, string? OldValue, string? NewValue);
