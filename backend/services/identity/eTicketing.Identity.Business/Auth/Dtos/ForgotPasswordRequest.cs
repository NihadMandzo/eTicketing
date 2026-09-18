namespace eTicketing.Identity.Business.Auth;

public record ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}
