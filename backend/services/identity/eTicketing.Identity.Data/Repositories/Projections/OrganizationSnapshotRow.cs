namespace eTicketing.Identity.Data.Repositories;

/// <summary>Projection, not an entity — one organization's outward-facing details, with
/// SuperAdminEmail folded in from the Users table. Lives here rather than being the Contracts event
/// type for the same reason SearchAsync takes a BaseSearchObject: Data cannot reference Business,
/// and the mapping to the event belongs with the code that publishes it.</summary>
public record OrganizationSnapshotRow(
    Guid Id,
    string Name,
    string Address,
    string Email,
    string PhoneNumber,
    string? SuperAdminEmail,
    bool IsActive);
