namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Identity → eTicketing.Ticketing. The organization is gone; drop its snapshot row.
///
/// <para>Deliberately does not touch anything else in Ticketing. Tickets sold by a deleted
/// organization are still real sales that still belong in the totals — the reports simply fall back
/// to labelling the row "Nepoznata organizacija", the same way they already did when the old
/// synchronous lookup came back empty.</para>
/// </summary>
public record OrganizationDeleted(Guid OrganizationId, DateTime DeletedAt);
