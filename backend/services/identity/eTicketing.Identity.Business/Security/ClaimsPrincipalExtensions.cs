using System.Security.Claims;

namespace eTicketing.Identity.Business.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role)!;

    public static int? GetOrganizationId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("organizationId");
        return value is null ? null : int.Parse(value);
    }

    public static bool IsPlatformStaff(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin") || user.IsInRole("Admin");

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin");
}
