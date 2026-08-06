using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Auth;

/// <summary>The 3 role-based policies shared by Identity.Api and any other service that
/// authorizes by role — deduplicated from what used to be copy-pasted per-service.</summary>
public static class AuthorizationPolicyExtensions
{
    public static IServiceCollection AddPlatformAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("SuperAdminOnly", p => p.RequireRole("SuperAdmin"))
            .AddPolicy("PlatformStaff", p => p.RequireRole("Admin", "SuperAdmin"))
            .AddPolicy("Organizer", p => p.RequireRole("OrganizationSuperAdmin", "OrganizationAdmin", "Admin", "SuperAdmin"));

        return services;
    }
}
