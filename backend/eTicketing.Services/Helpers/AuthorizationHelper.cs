using eTicketing.Model.Enums;
using eTicketing.Services.Database;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Helpers;

public class AuthorizationHelper
{
    private readonly JwtHelper _jwtHelper;
    private readonly eTicketingDbContext _context;

    public AuthorizationHelper(JwtHelper jwtHelper, eTicketingDbContext context)
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

    public bool CanManageOrganization(int organizationId)
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

    public bool CanDeleteOrganization(int organizationId)
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

    public void ValidateOrganizationAccess(int organizationId)
    {
        if (!CanManageOrganization(organizationId))
        {
            throw new UnauthorizedAccessException("Nemate pristup ovoj organizaciji");
        }
    }
}
