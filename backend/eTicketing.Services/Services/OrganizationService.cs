using AutoMapper;
using eTicketing.Model.Enums;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Helpers;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services;

public class OrganizationService : BaseCRUDService<Organization, OrganizationResponse, 
    OrganizationSearchObject, OrganizationInsertRequest, OrganizationUpdateRequest>, IOrganizationService
{
    private readonly JwtHelper _jwtHelper;
    private readonly IAuthorizationService _authorizationService;

    public OrganizationService(
        eTicketingDbContext context, 
        IMapper mapper,
        JwtHelper jwtHelper,
        IAuthorizationService authorizationService) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
        _authorizationService = authorizationService;
    }

    protected override IQueryable<Organization> ApplyFilter(IQueryable<Organization> query, OrganizationSearchObject? search)
    {
        // SuperAdmin can see all organizations
        // Organization users can only see their own organization
        if (!_authorizationService.IsSuperAdmin())
        {
            var organizationId = _jwtHelper.GetOrganizationId();
            if (organizationId.HasValue)
            {
                query = query.Where(x => x.Id == organizationId.Value);
            }
            else
            {
                // No organization - return empty
                query = query.Where(x => false);
            }
        }

        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(x => 
                x.Name.Contains(search.FTS) || 
                (x.Description != null && x.Description.Contains(search.FTS)) ||
                (x.Email != null && x.Email.Contains(search.FTS)));
        }

        if (search?.IsActive.HasValue == true)
        {
            query = query.Where(x => x.IsActive == search.IsActive.Value);
        }

        query = query.OrderBy(x => x.Name);

        return query;
    }

    protected override async Task BeforeCreateAsync(Organization entity, OrganizationInsertRequest request, 
        CancellationToken cancellationToken)
    {
        // Only SuperAdmin can create organizations
        if (!_authorizationService.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Only SuperAdmin can create organizations");
        }

        // Validate unique organization name
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == entity.Name, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organization with name '{entity.Name}' already exists");
        }

        // Validate unique email
        var emailExists = await Context.Set<Organization>()
            .AnyAsync(x => x.Email == entity.Email, cancellationToken);
        
        if (emailExists)
        {
            throw new InvalidOperationException($"Organization with email '{entity.Email}' already exists");
        }

        entity.CreatedAt = DateTime.UtcNow;
        entity.IsActive = true;
    }

    protected override async Task AfterCreateAsync(Organization entity, OrganizationInsertRequest request, 
        CancellationToken cancellationToken)
    {
        // Create Organization SuperAdmin user with provided password
        PasswordHelper.CreatePasswordHash(request.AdminPassword, out string passwordHash, out string passwordSalt);

        var adminUser = new User
        {
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            Email = request.AdminEmail,
            Username = request.AdminUsername,
            PhoneNumber = request.AdminPhoneNumber,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            RoleId = (int)RoleType.OrganizationSuperAdmin,
            OrganizationId = entity.Id,
            IsActive = true,
            IsEmailVerified = true, // Auto-verify for admin created by SuperAdmin
            IsFirstLogin = true, // Force password change on first login
            CreatedAt = DateTime.UtcNow
        };

        Context.Set<User>().Add(adminUser);
        await Context.SaveChangesAsync(cancellationToken);
    }

    protected override async Task BeforeUpdateAsync(Organization entity, OrganizationUpdateRequest request, 
        CancellationToken cancellationToken)
    {
        // Check permissions
        await _authorizationService.ValidateOrganizationAccessAsync(entity.Id, cancellationToken);

        // Validate unique name (exclude current)
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organization with name '{entity.Name}' already exists");
        }

        entity.UpdatedAt = DateTime.UtcNow;
    }

    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Check permissions
        if (!await _authorizationService.CanDeleteOrganizationAsync(id, cancellationToken))
        {
            throw new UnauthorizedAccessException("You don't have permission to delete this organization");
        }

        var organization = await Context.Set<Organization>()
            .Include(o => o.Users)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            return false;
        }

        // Soft delete: deactivate organization and all its users
        organization.IsActive = false;
        organization.UpdatedAt = DateTime.UtcNow;

        foreach (var user in organization.Users)
        {
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await Context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OrganizationDetailResponse> GetByIdDetailedAsync(int id, 
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.ValidateOrganizationAccessAsync(id, cancellationToken);

        var organization = await Context.Set<Organization>()
            .Include(o => o.Users.Where(u => u.RoleId == (int)RoleType.OrganizationSuperAdmin || 
                                              u.RoleId == (int)RoleType.OrganizationAdmin))
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            throw new KeyNotFoundException($"Organization with id {id} not found");
        }

        var response = Mapper.Map<OrganizationDetailResponse>(organization);
        response.Administrators = Mapper.Map<List<UserResponse>>(organization.Users);
        response.UserCount = await Context.Set<User>()
            .CountAsync(u => u.OrganizationId == id, cancellationToken);

        return response;
    }

    public async Task<UserResponse> AddUserAsync(int organizationId, OrganizationUserRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Only Organization SuperAdmin can add users
        await _authorizationService.ValidateOrganizationAccessAsync(organizationId, cancellationToken);

        if (!_authorizationService.IsOrganizationSuperAdmin() && !_authorizationService.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Only Organization SuperAdmin can add users");
        }

        // Validate role
        if (request.RoleId != (int)RoleType.OrganizationSuperAdmin && 
            request.RoleId != (int)RoleType.OrganizationAdmin)
        {
            throw new InvalidOperationException("Invalid role for organization user");
        }

        // Check if email/username already exists
        var emailExists = await Context.Set<User>()
            .AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException($"User with email '{request.Email}' already exists");
        }

        var usernameExists = await Context.Set<User>()
            .AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException($"User with username '{request.Username}' already exists");
        }

        // Create password hash with provided password
        PasswordHelper.CreatePasswordHash(request.Password, out string passwordHash, out string passwordSalt);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Username = request.Username,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            RoleId = request.RoleId,
            OrganizationId = organizationId,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = true, // Force password change on first login
            CreatedAt = DateTime.UtcNow
        };

        Context.Set<User>().Add(user);
        await Context.SaveChangesAsync(cancellationToken);

        // Load role for response
        await Context.Entry(user).Reference(u => u.Role).LoadAsync(cancellationToken);
        return Mapper.Map<UserResponse>(user);
    }

    public async Task<bool> RemoveUserAsync(int organizationId, int userId, 
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.ValidateOrganizationAccessAsync(organizationId, cancellationToken);

        if (!await _authorizationService.CanManageUserAsync(userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You don't have permission to remove this user");
        }

        var user = await Context.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);

        if (user == null)
        {
            return false;
        }

        // Prevent removing yourself
        var currentUserId = _jwtHelper.GetUserId();
        if (currentUserId == userId)
        {
            throw new InvalidOperationException("You cannot remove yourself");
        }

        // Soft delete
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<UserResponse>> GetOrganizationUsersAsync(int organizationId, 
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.ValidateOrganizationAccessAsync(organizationId, cancellationToken);

        var users = await Context.Set<User>()
            .Include(u => u.Role)
            .Where(u => u.OrganizationId == organizationId && u.IsActive)
            .OrderBy(u => u.Role.Name)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<UserResponse>>(users);
    }
}
