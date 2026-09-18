namespace eTicketing.Identity.Business.Auth;

public record UpdateUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}
