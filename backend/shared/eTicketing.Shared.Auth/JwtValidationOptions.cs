namespace eTicketing.Shared.Auth;

/// <summary>
/// Bound from the "Jwt" config section — the same section every service already uses to
/// configure its JWT signing key, now extended with issuer/audience so tokens can be
/// validated as belonging to this platform (closes the previous "any service accepts any
/// other service's token" gap).
/// </summary>
public class JwtValidationOptions
{
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "eticketing-identity";
    public string Audience { get; set; } = "eticketing-services";
}
