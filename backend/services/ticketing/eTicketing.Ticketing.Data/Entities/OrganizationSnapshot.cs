using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// A read model, not a domain entity — Ticketing's local copy of the organization details it has to
/// be able to print. Sibling of <see cref="ProductSnapshot"/> and fed the same way, by events.
///
/// <para>It replaced a synchronous Ticketing→Identity HTTP call that was never in the architecture
/// doc's whitelist of cross-service calls. Two things needed it: the Izvještaji reports, which label
/// their rows with an organization's name and address, and the deleted-product notice, which tells a
/// buyer whose event was cancelled who to ask for their money back.</para>
///
/// <para>A missing row is treated exactly as the old HTTP failure was — "Nepoznata organizacija",
/// and a cancellation email sent without a contact block. That was a deliberate choice then and
/// stays one now: a buyer being told their event is off matters more than the address being on it.
/// This is the opposite default from <see cref="ProductSnapshot"/>, where an unknown row blocks the
/// sale; the difference is that a wrong label is cosmetic and a wrong sale is not.</para>
/// </summary>
public class OrganizationSnapshot : BaseEntity
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    /// <summary>The organization's own contact address — not a staff member's. The organizer took
    /// the payment, so the organizer issues the refund.</summary>
    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Preferred recipient for "platform staff removed your product", falling back to
    /// <see cref="Email"/>. Null for an organization that has no OrganizationSuperAdmin — which the
    /// invariant says cannot happen, and which this reads defensively anyway.</summary>
    public string? SuperAdminEmail { get; set; }

    public bool IsActive { get; set; }

    /// <summary>When Identity produced this state; see ProductSnapshot.ChangedAt for why UpdatedAt
    /// cannot serve instead.</summary>
    public DateTime ChangedAt { get; set; }
}
