namespace eTicketing.Ticketing.Business.GateDevices;

/// <summary>Same shape for create and update. <paramref name="AllSectors"/> and
/// <paramref name="SectorIds"/> are mutually exclusive: exactly one of them defines the scope, and
/// the validator rejects a request that sets neither.</summary>
public sealed record UpsertGateDeviceRequest
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool AllSectors { get; init; }
    public List<Guid> SectorIds { get; init; } = [];
    public bool IsActive { get; init; } = true;
}
