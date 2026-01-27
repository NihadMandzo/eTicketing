using eTicketing.Model.Enums;
using eTicketing.Services.Database;
using eTicketing.Services.Helpers;
using eTicketing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly JwtHelper _jwtHelper;
    private readonly eTicketingDbContext _context;

    public AuthorizationService(JwtHelper jwtHelper, eTicketingDbContext context)
    {
        _jwtHelper = jwtHelper;
        _context = context;
    }

    public bool IsSuperAdmin()
    {
        return _jwtHelper.HasRole("SuperAdmin");
    }

    public bool IsOrganizationSuperAdmin()
    {
        return _jwtHelper.HasRole("OrganizationSuperAdmin");
    }

    public bool IsOrganizationAdmin()
    {
        return _jwtHelper.HasRole("OrganizationAdmin");
    }

    public async Task<bool> CanManageOrganizationAsync(int organizationId, 
        CancellationToken cancellationToken = default)
    {
        // SuperAdmin can manage any organization
        if (IsSuperAdmin())
        {
            return true;
        }

        // Organization SuperAdmin can manage their own organization
        if (IsOrganizationSuperAdmin())
        {
            var userOrgId = _jwtHelper.GetOrganizationId();
            return userOrgId.HasValue && userOrgId.Value == organizationId;
        }

        return false;
    }

    public async Task<bool> CanDeleteOrganizationAsync(int organizationId, 
        CancellationToken cancellationToken = default)
    {
        // SuperAdmin can delete any organization
        if (IsSuperAdmin())
        {
            return true;
        }

        // Organization SuperAdmin can delete their own organization
        if (IsOrganizationSuperAdmin())
        {
            var userOrgId = _jwtHelper.GetOrganizationId();
            return userOrgId.HasValue && userOrgId.Value == organizationId;
        }

        return false;
    }

    public async Task<bool> CanManageUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        
        if (user == null)
        {
            return false;
        }

        // SuperAdmin can manage any user
        if (IsSuperAdmin())
        {
            return true;
        }

        // Organization SuperAdmin can manage users in their organization
        if (IsOrganizationSuperAdmin())
        {
            var userOrgId = _jwtHelper.GetOrganizationId();
            return userOrgId.HasValue && user.OrganizationId == userOrgId.Value;
        }

        return false;
    }

    public async Task ValidateOrganizationAccessAsync(int organizationId, 
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageOrganizationAsync(organizationId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You don't have access to this organization");
        }
    }
}
