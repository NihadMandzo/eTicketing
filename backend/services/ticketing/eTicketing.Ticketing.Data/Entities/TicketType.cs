using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// An optional named price tier on a Sector (e.g. "Odrasli"/"Djeca"/"Studenti" on a museum day's
/// Sector, or "VIP-Odrasli"/"VIP-Djeca" on an event's VIP Sector). Has no Draft/Published lifecycle
/// of its own — it inherits its parent Sector's. A Sector with zero TicketTypes behaves exactly as
/// before this feature (one implicit price = Sector.Price); one with TicketTypes requires buyers to
/// pick quantities per named type, all drawing against that Sector's single shared Capacity pool
/// (no separate capacity per type — see PurchaseService).
/// </summary>
public class TicketType : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
