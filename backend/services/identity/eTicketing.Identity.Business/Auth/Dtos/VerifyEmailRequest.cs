namespace eTicketing.Identity.Business.Auth;

public record VerifyEmailRequest
{
    public string Code { get; init; } = string.Empty;
}
