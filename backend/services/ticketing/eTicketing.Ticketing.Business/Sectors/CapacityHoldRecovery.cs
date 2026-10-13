using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Sectors;

/// <summary>
/// The one way to take a capacity hold: try it, and if Redis has no counter for this sector/date,
/// rebuild the counter from the database and try once more.
///
/// <para>Without the rebuild, a counter Redis lost would start again from zero and every seat
/// already sold would be sold a second time. With it, the cost is one database count the first
/// time a counter is used (or after Redis lost it) and nothing on any other hold.</para>
/// </summary>
public static class CapacityHoldRecovery
{
    public static async Task<HoldResult> TryHoldRecoveringAsync(
        this ISectorCapacityLock capacityLock,
        ISoldCapacityCounter soldCapacity,
        Sector sector,
        int quantity,
        DateOnly? date,
        TimeSpan ttl,
        Guid? ownerId,
        CancellationToken ct = default)
    {
        var hold = await capacityLock.TryHoldAsync(sector.Id, sector.Capacity, quantity, date, ttl, ownerId, ct);
        if (!hold.CounterMissing)
            return hold;

        var occupied = await soldCapacity.CountAsync(sector, date, ct);
        await capacityLock.SeedAsync(sector.Id, date, occupied, ct);

        return await capacityLock.TryHoldAsync(sector.Id, sector.Capacity, quantity, date, ttl, ownerId, ct);
    }
}
