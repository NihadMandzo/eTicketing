namespace eTicketing.Ticketing.Business.Security;

/// <summary><paramref name="ApiKey"/> is the only time the plaintext exists — it is returned to the
/// organizer once and then only its hash survives.</summary>
public sealed record GateDeviceKey(string ApiKey, string KeyHash, string KeyPrefix);
