namespace eTicketing.Identity.Business.Auth;

public record LoginRequest
{
    public string EmailOrUsername { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
