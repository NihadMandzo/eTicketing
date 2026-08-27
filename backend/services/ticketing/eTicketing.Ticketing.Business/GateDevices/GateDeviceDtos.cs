using eTicketing.Contracts.Pagination;

namespace eTicketing.Ticketing.Business.GateDevices;

public sealed record GateDeviceQuery : BaseSearchObject
{
    public Guid? ProductId { get; init; }
}

/// <summary>One sector a gate is scoped to.</summary>
public sealed record GateDeviceSectorResponse(Guid Id, string Name);

/// <summary>The back-office view of a registered gate. Carries <c>KeyPrefix</c> but never the key —
/// the plaintext exists only in <see cref="GateDeviceCreatedResponse"/>, once.</summary>
public sealed record GateDeviceResponse(
    Guid Id,
    Guid ProductId,
    string Name,
    string KeyPrefix,
    bool AllSectors,
    bool IsActive,
    DateTime? LastSeenAt,
    DateTime CreatedAt,
    IReadOnlyList<GateDeviceSectorResponse> Sectors);

/// <summary>Returned by create and rotate-key, and by nothing else. <paramref name="ApiKey"/> is
/// shown to the organizer exactly once — the server keeps only its hash from here on, so a lost key
/// is rotated, never recovered.</summary>
public sealed record GateDeviceCreatedResponse(GateDeviceResponse Device, string ApiKey);

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

/// <summary>What the firmware pulls at boot and re-pulls periodically, so a scope change made in
/// the back-office reaches a gate without anyone re-flashing it. <paramref name="ServerTime"/> lets
/// a device with no RTC log meaningful timestamps.</summary>
public sealed record GateConfigResponse(
    Guid DeviceId,
    string DeviceName,
    Guid ProductId,
    string ProductName,
    DateTime? ProductDate,
    bool AllSectors,
    IReadOnlyList<GateDeviceSectorResponse> Sectors,
    DateTime ServerTime);

/// <summary>The device-facing scan body. Deliberately carries nothing but the scanned string: the
/// product and sector scope are read from the device's own row, so a tampered device cannot widen
/// what it admits by lying about which gate it is.</summary>
public sealed record GateValidateRequest
{
    public string Code { get; init; } = string.Empty;
}
