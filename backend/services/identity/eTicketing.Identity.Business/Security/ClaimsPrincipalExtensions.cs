using System.Security.Claims;

namespace eTicketing.Identity.Business.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role)!;

    public static Guid? GetOrganizationId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("organizationId");
        return value is null ? null : Guid.Parse(value);
    }

    public static bool IsPlatformStaff(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin") || user.IsInRole("Admin");

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin");
}
