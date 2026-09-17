namespace eTicketing.Ticketing.Business.GateDevices;

/// <summary>The device-facing scan body. Deliberately carries nothing but the scanned string: the
/// product and sector scope are read from the device's own row, so a tampered device cannot widen
/// what it admits by lying about which gate it is.</summary>
public sealed record GateValidateRequest
{
    public string Code { get; init; } = string.Empty;
}
