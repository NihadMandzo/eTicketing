using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// One physical scanner standing at one gate — in practice an ESP32-CAM reading ticket QR codes
/// (see IoT/). The row, not the firmware, is what decides which tickets that gate admits:
/// <see cref="ProductId"/> plus either <see cref="AllSectors"/> or the <see cref="Sectors"/> set.
///
/// That split is the whole point of the entity. A device authenticates with a bearer-style key and
/// then POSTs nothing but the scanned string; the product and sector scope are read back from here.
/// So flashing tampered firmware onto a gate cannot widen what it admits, and re-scoping a gate
/// ("this door now also takes Loža") is a back-office edit that the device picks up on its next
/// config refresh — no re-flash, no redeploy.
///
/// <see cref="KeyHash"/> holds SHA-256 of the API key and the plaintext is shown exactly once, at
/// create and at rotate. The key is 192 bits of CSPRNG output, so a plain hash is the right
/// primitive here — a password KDF exists to slow down guessing at low-entropy secrets, and there
/// is nothing to guess at.
/// </summary>
public class GateDevice : BaseEntity
{
    public Guid Id { get; set; }

    // Denormalized from the owning Product at creation time, exactly like Sector.OrganizationId —
    // so the tenancy check on every device call is a local column read, not a Catalog round-trip.
    public Guid OrganizationId { get; set; }

    // Cross-service ref to Catalog.Product — plain Guid column, no FK/navigation across the
    // service boundary, same convention as Sector.ProductId.
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex, lowercase) of the API key. The key itself is never persisted.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First characters of the key, kept in the clear purely so the back-office list can
    /// tell two devices apart after the plaintext is gone. Not a secret and not usable to
    /// authenticate.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>True for a main-entrance device that admits every sector of <see cref="ProductId"/>.
    /// Deliberately an explicit flag rather than "an empty Sectors list means all": an empty list
    /// is far more likely to be a mistake than an intention, and the failure mode of guessing wrong
    /// is a gate that admits everyone.</summary>
    public bool AllSectors { get; set; }

    /// <summary>Deactivating is the reversible kill switch for a lost device — it fails auth
    /// immediately without destroying the row's validation history. Deleting is still a hard
    /// delete, per the platform's no-soft-delete rule.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Last time this device successfully authenticated. Surfaced in the back-office so an
    /// organizer can tell a working gate from a dead one before the doors open.</summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>The organizer who registered this gate. Written to Ticket.ValidatedByUserId when
    /// the device burns a ticket, so every admission still traces to an accountable person.</summary>
    public Guid CreatedByUserId { get; set; }

    public ICollection<GateDeviceSector> Sectors { get; set; } = new List<GateDeviceSector>();
}
