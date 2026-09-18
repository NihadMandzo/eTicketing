namespace eTicketing.Identity.Business.Auth;

/// <summary>The access/refresh tokens never appear in a response body — they're written
/// straight to httpOnly cookies (see AuthEndpoints). This is the client-facing payload.</summary>
public record LoginResponse(UserResponse User);
