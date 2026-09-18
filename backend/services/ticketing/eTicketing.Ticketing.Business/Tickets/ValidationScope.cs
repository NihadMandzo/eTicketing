namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// Who is allowed through this particular door, resolved before any ticket is looked at.
///
/// It exists so the gate logic has exactly one shape to check against, whoever is asking: an
/// organizer holding a phone (claims) and an unattended ESP32-CAM bolted next to a turnstile
/// (a GateDevice row) reduce to the same five facts. Nothing below this point knows or cares which
/// one it is serving.
/// </summary>
/// <param name="BypassOrganizationCheck">PlatformStaff only. Deliberately a separate flag rather
/// than "a null OrganizationId means skip": an organizer whose token carries no organizationId must
/// be turned away, not handed a platform-wide override, and overloading null onto one field makes
/// those two cases indistinguishable.</param>
/// <param name="SectorIds">Null means every sector of <paramref name="ProductId"/> — a main
/// entrance. A non-empty set means this door only takes those sectors.</param>
internal readonly record struct ValidationScope(
    bool BypassOrganizationCheck,
    Guid? OrganizationId,
    Guid ProductId,
    IReadOnlySet<Guid>? SectorIds,
    Guid ValidatedByUserId,
    Guid? ValidatedByDeviceId);
