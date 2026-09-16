namespace eTicketing.Notifications.Email.Templates;

/// <summary><paramref name="Field"/> arrives already translated (eTicketing.Catalog's
/// ProductChangeDetector resolves the Bosnian label, because the domain meaning of a field lives
/// in the service that owns it). This template never maps field names itself.</summary>
public sealed record ProductChangeLine(string Field, string? OldValue, string? NewValue);
