using System.Security.Claims;

namespace eTicketing.Ticketing.Business.Security;

/// <summary>Mirrors eTicketing.Identity.Business.Security.ClaimsPrincipalExtensions (and
/// eTicketing.Catalog.Business's copy) — duplicated per-service rather than shared, following the
/// convention Identity already established.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // JwtTokenGenerator (eTicketing.Identity) already mints ClaimTypes.Email into every token, so
    // Ticketing can denormalize Ticket.UserEmail straight off the caller's claims — no cross-
    // service call to Identity needed on the purchase-critical path.
    public static string GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email)!;

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role)!;

    public static Guid? GetOrganizationId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("organizationId");
        return value is null ? null : Guid.Parse(value);
    }

    public static bool IsPlatformStaff(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin") || user.IsInRole("Admin");
}
