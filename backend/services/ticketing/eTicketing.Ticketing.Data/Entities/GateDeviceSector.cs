using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>Join row letting one device cover several sectors at once — the "Ulaz A takes VIP and
/// Loža" case. Kept as an entity rather than a shadow join table so it derives from BaseEntity and
/// picks up the audit timestamps every other row in this schema carries.</summary>
public class GateDeviceSector : BaseEntity
{
    public Guid Id { get; set; }

    public Guid GateDeviceId { get; set; }
    public GateDevice? GateDevice { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }
}
