namespace eTicketing.Ticketing.Business.Tickets;

/// <summary><paramref name="Code"/> is whatever the scanner read — a full signed QR payload, or a
/// bare ticket GUID typed into the manual-entry fallback. <paramref name="ProductId"/> is the
/// product the organizer opened the scanner for: validation is always "is this ticket good for
/// THIS product", never "is this ticket good in general".</summary>
public sealed record ValidateTicketRequest
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;

    /// <summary>Optional narrowing to specific sectors of <see cref="ProductId"/> — "this door only
    /// takes VIP and Loža". Null or empty keeps the original behavior of admitting any sector of
    /// the product, which is what the mobile scanner (which opens on a product, not a door) sends.
    /// A registered gate device does not use this field at all: its scope comes from its own row,
    /// never from the wire.</summary>
    public List<Guid>? SectorIds { get; init; }
}
