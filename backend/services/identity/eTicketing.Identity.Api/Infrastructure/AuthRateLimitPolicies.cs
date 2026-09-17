namespace eTicketing.Identity.Api.Infrastructure;

/// <summary>
/// The rate-limiter policy names, in one place so AuthEndpoints and the registration in
/// IdentityServiceCollectionExtensions cannot drift — a route asking for a policy that was never
/// registered throws at request time, not at startup.
///
/// Every one of these partitions on the caller's IP, which is only the caller's IP because
/// UseForwardedHeaders runs first — see eTicketing.Contracts.Hosting.ForwardedHeadersExtensions.
/// </summary>
public static class AuthRateLimitPolicies
{
    /// <summary>Outbound-email throttle: /auth/forgot-password and /auth/resend-verification-email.</summary>
    public const string EmailSending = "email-sending";

    /// <summary>Credential-stuffing and signup-flood throttle: /auth/login and /auth/register.</summary>
    public const string AuthAttempts = "auth-attempts";
}
