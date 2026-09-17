namespace eTicketing.Identity.Business.Auth;

/// <summary>Internal Business→Api handoff only — never serialized directly.</summary>
public record LoginResult(UserResponse User, string AccessToken, string RefreshToken);
