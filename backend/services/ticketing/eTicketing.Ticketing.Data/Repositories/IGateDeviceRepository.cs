using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface IGateDeviceRepository : IRepository<GateDevice, Guid>
{
    /// <summary>The authentication path, hit once per scan. Includes Sectors because the caller
    /// immediately needs the scope to validate against, and tracked (not AsNoTracking) because the
    /// same instance gets its LastSeenAt stamped in the same unit of work.</summary>
    Task<GateDevice?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);

    /// <summary>Paged back-office list. A null organizationId means PlatformStaff, who see every
    /// organization's devices — same convention as TicketRepository.GetValidationCountsAsync.</summary>
    Task<PagedResult<GateDevice>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, Guid? productId, CancellationToken ct = default);

    /// <summary>GetByIdAsync can't eager-load, and every write path needs the existing sector set
    /// in order to diff it against the requested one.</summary>
    Task<GateDevice?> GetByIdWithSectorsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Which of these sector ids actually belong to this product. Used to reject a device
    /// scoped to another product's sector before it is ever written.</summary>
    Task<List<Guid>> GetSectorIdsForProductAsync(Guid productId, IReadOnlyList<Guid> sectorIds, CancellationToken ct = default);

    /// <summary>Names of the device's sectors, for the config the firmware fetches at boot.</summary>
    Task<List<(Guid Id, string Name)>> GetSectorNamesAsync(IReadOnlyList<Guid> sectorIds, CancellationToken ct = default);

    /// <summary>
    /// Add/remove one scope row explicitly rather than leaving it to navigation fixup.
    ///
    /// This is not ceremony. A GateDeviceSector carries a client-assigned Guid key, and when an
    /// instance with its key already set turns up inside an <b>already-tracked</b> parent's
    /// collection, EF Core reads that as "an existing row" and emits an UPDATE — which matches
    /// nothing and throws DbUpdateConcurrencyException. Going through the DbSet states the intent
    /// outright and does not depend on that inference.
    /// </summary>
    void AddSector(GateDeviceSector sector);

    void RemoveSector(GateDeviceSector sector);
}
