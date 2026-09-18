namespace eTicketing.Ticketing.Business.GateDevices;

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
