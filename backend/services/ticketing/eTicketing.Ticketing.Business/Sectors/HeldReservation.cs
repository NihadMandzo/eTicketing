namespace eTicketing.Ticketing.Business.Sectors;

/// <summary>What a holdId actually reserved, reconstructed server-side from Redis. PurchaseService
/// resolves SectorId/Quantity exclusively from this — never from client-supplied values — so a
/// caller cannot present one sector's hold together with a different sector/quantity claim and
/// have capacity silently not decremented for what was actually sold.</summary>
/// <param name="OwnerId">Who holds it, so release and purchase can refuse a hold id that belongs to
/// somebody else. Null means the hold has no owner and anyone may consume it: either a system hold
/// (the organizer print batch, which has no single buyer) or a hold minted by a deployment older
/// than the owner-carrying holdinfo format — see RedisSectorCapacityLock.TryParseHoldInfo.</param>
public record HeldReservation(Guid SectorId, DateOnly? Date, int Quantity, Guid? OwnerId);
