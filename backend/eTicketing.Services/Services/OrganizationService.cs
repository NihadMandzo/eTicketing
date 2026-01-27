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
    private readonly AuthorizationHelper _authorizationHelper;

    public OrganizationService(
        eTicketingDbContext context, 
        IMapper mapper,
        JwtHelper jwtHelper,
        AuthorizationHelper authorizationHelper) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
        _authorizationHelper = authorizationHelper;
    }

    protected override IQueryable<Organization> ApplyFilter(IQueryable<Organization> query, OrganizationSearchObject? search)
    {
        // SuperAdmin can see all organizations
        // Organization users can only see their own organization
        if (!_authorizationHelper.IsSuperAdmin())
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
        if (!_authorizationHelper.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Samo SuperAdmin može kreirati organizacije");
        }

        // Validate unique organization name
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == entity.Name, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organizacija sa imenom '{entity.Name}' već postoji");
        }

        // Validate unique email
        var emailExists = await Context.Set<Organization>()
            .AnyAsync(x => x.Email == entity.Email, cancellationToken);
        
        if (emailExists)
        {
            throw new InvalidOperationException($"Organizacija sa emailom '{entity.Email}' već postoji");
        }

        entity.CreatedAt = DateTime.UtcNow;
        entity.IsActive = true;
    }

    protected override async Task AfterCreateAsync(Organization entity, OrganizationInsertRequest request, 
        CancellationToken cancellationToken)
    {
        // Validate unique email
        var emailExists = await Context.Set<User>()
            .AnyAsync(u => u.Email == request.AdminEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException($"Korisnik sa emailom '{request.AdminEmail}' već postoji");
        }

        // Validate unique username
        var usernameExists = await Context.Set<User>()
            .AnyAsync(u => u.Username == request.AdminUsername, cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException($"Korisnik sa korisničkim imenom '{request.AdminUsername}' već postoji");
        }

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
        _authorizationHelper.ValidateOrganizationAccess(entity.Id);

        // Validate unique name (exclude current)
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organizacija sa imenom '{entity.Name}' već postoji");
        }

        entity.UpdatedAt = DateTime.UtcNow;
    }

    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Check permissions
        if (!_authorizationHelper.CanDeleteOrganization(id))
        {
            throw new UnauthorizedAccessException("Nemate dozvolu za brisanje ove organizacije");
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
        _authorizationHelper.ValidateOrganizationAccess(id);

        var organization = await Context.Set<Organization>()
            .Include(o => o.Users.Where(u => u.RoleId == (int)RoleType.OrganizationSuperAdmin || 
                                              u.RoleId == (int)RoleType.OrganizationAdmin))
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            throw new KeyNotFoundException($"Organizacija sa ID-om {id} nije pronađena");
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
        _authorizationHelper.ValidateOrganizationAccess(organizationId);

        if (!_authorizationHelper.IsOrganizationSuperAdmin() && !_authorizationHelper.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Samo SuperAdmin organizacije može dodavati korisnike");
        }

        // Validate role
        if (request.RoleId != (int)RoleType.OrganizationSuperAdmin && 
            request.RoleId != (int)RoleType.OrganizationAdmin)
        {
            throw new InvalidOperationException("Neispravna uloga za korisnika organizacije");
        }

        // Check if email/username already exists
        var emailExists = await Context.Set<User>()
            .AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException($"Korisnik sa emailom '{request.Email}' već postoji");
        }

        var usernameExists = await Context.Set<User>()
            .AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException($"Korisnik sa korisničkim imenom '{request.Username}' već postoji");
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
        _authorizationHelper.ValidateOrganizationAccess(organizationId);

        if (!await _authorizationHelper.CanManageUserAsync(userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Nemate dozvolu za uklanjanje ovog korisnika");
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
            throw new InvalidOperationException("Ne možete ukloniti sami sebe");
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
        _authorizationHelper.ValidateOrganizationAccess(organizationId);

        var users = await Context.Set<User>()
            .Include(u => u.Role)
            .Where(u => u.OrganizationId == organizationId && u.IsActive)
            .OrderBy(u => u.Role.Name)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<UserResponse>>(users);
    }
}
