namespace eTicketing.Ticketing.Business.GateDevices;

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
