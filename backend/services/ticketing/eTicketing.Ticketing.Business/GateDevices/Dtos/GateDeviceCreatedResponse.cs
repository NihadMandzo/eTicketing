namespace eTicketing.Ticketing.Business.GateDevices;

/// <summary>Returned by create and rotate-key, and by nothing else. <paramref name="ApiKey"/> is
/// shown to the organizer exactly once — the server keeps only its hash from here on, so a lost key
/// is rotated, never recovered.</summary>
public sealed record GateDeviceCreatedResponse(GateDeviceResponse Device, string ApiKey);
