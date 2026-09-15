using System.Security.Claims;

namespace eTicketing.Contracts.Security;

/// <summary>
/// The single, deduplicated set of claim readers every service shares — previously this exact
/// block was copy-pasted into Identity.Business, Catalog.Business and Ticketing.Business, and had
/// already started to drift (only Identity had <see cref="IsSuperAdmin"/>, only Ticketing had
/// <see cref="GetEmail"/>).
/// Lives in eTicketing.Contracts rather than eTicketing.Shared.Auth deliberately: every .Business
/// project already references Contracts, and this class needs nothing beyond
/// <see cref="System.Security.Claims"/>. Shared.Auth carries a FrameworkReference on
/// Microsoft.AspNetCore.App plus the JwtBearer package, which Ticketing.Business (no
/// FrameworkReference at all today) would otherwise inherit wholesale.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>The non-throwing <see cref="GetUserId"/>, for code that decides an authorization
    /// question from the caller's identity and must not turn a principal without a usable
    /// <c>sub</c> into a 500. Every principal minted by JwtTokenGenerator carries one, but not
    /// every principal reaching a service was minted there — eTicketing.Ticketing also registers
    /// the GateDevice scheme, whose principal identifies a turnstile rather than an account.
    /// Returns null rather than Guid.Empty so a missing claim can never compare equal to a real
    /// owner id.</summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    // JwtTokenGenerator (eTicketing.Identity) already mints ClaimTypes.Email into every token, so
    // Ticketing can denormalize Ticket.UserEmail straight off the caller's claims — no cross-
    // service call to Identity needed on the purchase-critical path. The null-forgiving operator
    // is only safe because of that guarantee: don't call this for a principal minted elsewhere.
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

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.IsInRole("SuperAdmin");
}
