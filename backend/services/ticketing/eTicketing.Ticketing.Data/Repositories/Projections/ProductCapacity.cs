namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Projection, not an entity — one row per product with its total published capacity and
/// how many sectors that came from (the "N sektora" half of the organizer-facing product meta).</summary>
public record ProductCapacity(Guid ProductId, int Capacity, int SectorCount);
