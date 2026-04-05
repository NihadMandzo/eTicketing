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
using Microsoft.Extensions.Logging;

namespace eTicketing.Services.Services;

public class OrganizationService : BaseCRUDService<Organization, OrganizationResponse, 
    OrganizationSearchObject, OrganizationInsertRequest, OrganizationUpdateRequest>, IOrganizationService
{
    private readonly JwtHelper _jwtHelper;
    private readonly AuthorizationHelper _authorizationHelper;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<OrganizationService> _logger;
    private const string OrganizationLogosContainer = "organization-logos";

    public OrganizationService(
        eTicketingDbContext context, 
        IMapper mapper,
        JwtHelper jwtHelper,
        AuthorizationHelper authorizationHelper,
        IBlobStorageService blobStorageService,
        ILogger<OrganizationService> logger) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
        _authorizationHelper = authorizationHelper;
        _blobStorageService = blobStorageService;
        _logger = logger;
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
                // User has no organization - return empty result set
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

        // Include Image for logo URL mapping
        query = query.Include(x => x.Image);

        query = query.OrderBy(x => x.Name);

        return query;
    }

    public override async Task<OrganizationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Validate organization access before retrieving
        _authorizationHelper.ValidateOrganizationAccess(id);

        var organizationData = await Context.Set<Organization>()
            .Include(o => o.Image)
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Organization = o,
                UserCount = o.Users.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationData == null)
            return null;

        var response = Mapper.Map<OrganizationResponse>(organizationData.Organization);
        response.UserCount = organizationData.UserCount;

        return response;
    }

    public override async Task<OrganizationResponse> CreateAsync(OrganizationInsertRequest request, CancellationToken cancellationToken = default)
    {
        // Only SuperAdmin can create organizations
        if (!_authorizationHelper.IsSuperAdmin())
        {
            throw new UnauthorizedAccessException("Nemate dozvolu za kreiranje organizacije");
        }

        // Validate unique organization name
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == request.Name, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organizacija sa imenom '{request.Name}' već postoji");
        }

        // Validate unique email
        var emailExists = await Context.Set<Organization>()
            .AnyAsync(x => x.Email == request.Email, cancellationToken);
        
        if (emailExists)
        {
            throw new InvalidOperationException($"Organizacija sa emailom '{request.Email}' već postoji");
        }

        // Validate unique admin email
        var adminEmailExists = await Context.Set<User>()
            .AnyAsync(u => u.Email == request.AdminEmail, cancellationToken);
        if (adminEmailExists)
        {
            throw new InvalidOperationException($"Korisnik sa emailom '{request.AdminEmail}' već postoji");
        }

        // Validate unique admin username
        var adminUsernameExists = await Context.Set<User>()
            .AnyAsync(u => u.Username == request.AdminUsername, cancellationToken);
        if (adminUsernameExists)
        {
            throw new InvalidOperationException($"Korisnik sa korisničkim imenom '{request.AdminUsername}' već postoji");
        }

        var entity = new Organization();
        Mapper.Map(request, entity);

        string? logoUrl = null;
        // Upload logo if provided
        if (request.Logo != null)
        {
            logoUrl = await _blobStorageService.UploadAsync(request.Logo, OrganizationLogosContainer);
            entity.Image = new Image { ImageUrl = logoUrl };
        }

        entity.CreatedAt = DateTime.UtcNow;
        entity.IsActive = true;

        try
        {
            Context.Set<Organization>().Add(entity);
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (logoUrl != null)
            {
                await _blobStorageService.DeleteAsync(logoUrl, OrganizationLogosContainer);
            }
            throw;
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

        return MapToResponse(entity);
    }

    public override async Task<OrganizationResponse> UpdateAsync(int id, OrganizationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        // Check permissions
        _authorizationHelper.ValidateOrganizationAccess(id);

        var entity = await Context.Set<Organization>().FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"Entitet sa ID-om {id} nije pronađen");

        // Validate unique name (exclude current)
        var exists = await Context.Set<Organization>()
            .AnyAsync(x => x.Name == request.Name && x.Id != id, cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Organizacija sa imenom '{request.Name}' već postoji");
        }

        // Validate unique email (exclude current)
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await Context.Set<Organization>()
                .AnyAsync(x => x.Email == request.Email && x.Id != id, cancellationToken);
            
            if (emailExists)
            {
                throw new InvalidOperationException($"Organizacija sa emailom '{request.Email}' već postoji");
            }
        }

        Mapper.Map(request, entity);
        entity.UpdatedAt = DateTime.UtcNow;

        // Load existing image
        await Context.Entry(entity)
            .Reference(e => e.Image)
            .LoadAsync(cancellationToken);

        string? oldLogoUrl = entity.Image?.ImageUrl;
        string? newLogoUrl = null;

        // Handle logo removal
        if (request.RemoveLogo && entity.Image != null)
        {
            Context.Set<Image>().Remove(entity.Image);
            entity.Image = null;
            entity.ImageId = null;
        }

        // Handle new logo upload
        if (request.Logo != null)
        {
            newLogoUrl = await _blobStorageService.UploadAsync(request.Logo, OrganizationLogosContainer);

            if (entity.Image != null)
            {
                entity.Image.ImageUrl = newLogoUrl;
            }
            else
            {
                entity.Image = new Image { ImageUrl = newLogoUrl };
            }
        }

        try
        {
            Context.Set<Organization>().Update(entity);
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (newLogoUrl != null)
            {
                await _blobStorageService.DeleteAsync(newLogoUrl, OrganizationLogosContainer);
            }
            throw;
        }

        // Delete old blob after successful DB commit
        if (oldLogoUrl != null && (request.RemoveLogo || request.Logo != null))
        {
            try
            {
                await _blobStorageService.DeleteAsync(oldLogoUrl, OrganizationLogosContainer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old logo blob {BlobUrl} for organization {OrgId}. Blob may be orphaned.",
                    oldLogoUrl, entity.Id);
            }
        }

        return MapToResponse(entity);
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
            .Include(o => o.Image)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            return false;
        }

        // Capture logo URL before soft delete
        string? logoUrl = organization.Image?.ImageUrl;

        // Soft delete: deactivate organization and all its users
        organization.IsActive = false;
        organization.UpdatedAt = DateTime.UtcNow;

        foreach (var user in organization.Users)
        {
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
        }

        // Remove logo image record and blob
        if (organization.Image != null)
        {
            Context.Set<Image>().Remove(organization.Image);
            organization.Image = null;
            organization.ImageId = null;
        }

        await Context.SaveChangesAsync(cancellationToken);

        // Delete logo blob after successful DB commit
        if (logoUrl != null)
        {
            try
            {
                await _blobStorageService.DeleteAsync(logoUrl, OrganizationLogosContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete logo blob {BlobUrl} for deleted organization {OrgId}. Blob may be orphaned.",
                    logoUrl, id);
            }
        }

        return true;
    }

    public async Task<OrganizationDetailResponse> GetByIdDetailedAsync(int id, 
        CancellationToken cancellationToken = default)
    {
        _authorizationHelper.ValidateOrganizationAccess(id);

        // Optimized: Use a single query with projection to get organization, admins, and user count
        var organizationData = await Context.Set<Organization>()
            .Include(o => o.Image)
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Organization = o,
                Administrators = o.Users
                    .Where(u => u.RoleId == (int)RoleType.OrganizationSuperAdmin || 
                               u.RoleId == (int)RoleType.OrganizationAdmin)
                    .Select(u => new { User = u, Role = u.Role })
                    .ToList(),
                UserCount = o.Users.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationData == null)
        {
            throw new KeyNotFoundException($"Organizacija sa ID-om {id} nije pronađena");
        }

        var response = Mapper.Map<OrganizationDetailResponse>(organizationData.Organization);
        
        // Map administrators with their roles
        response.Administrators = organizationData.Administrators
            .Select(a => 
            {
                var userResponse = Mapper.Map<UserResponse>(a.User);
                userResponse.RoleName = a.Role.Name;
                return userResponse;
            })
            .ToList();
        
        response.UserCount = organizationData.UserCount;

        return response;
    }

    public async Task<UserResponse> AddUserAsync(int organizationId, OrganizationUserRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Validate organization access and ensure user is SuperAdmin or Organization SuperAdmin
        _authorizationHelper.ValidateOrganizationAccess(organizationId);

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

        // Prevent removing the last OrganizationSuperAdmin
        if (user.RoleId == (int)RoleType.OrganizationSuperAdmin)
        {
            var superAdminCount = await Context.Set<User>()
                .CountAsync(u => u.OrganizationId == organizationId && 
                               u.RoleId == (int)RoleType.OrganizationSuperAdmin && 
                               u.IsActive, 
                           cancellationToken);

            if (superAdminCount <= 1)
            {
                throw new InvalidOperationException("Ne možete ukloniti poslednjeg SuperAdmina organizacije");
            }
        }

        // Soft delete
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PagedResponse<UserResponse>> GetOrganizationUsersAsync(int organizationId, 
        BaseSearchObject? search = null, CancellationToken cancellationToken = default)
    {
        _authorizationHelper.ValidateOrganizationAccess(organizationId);

        var query = Context.Set<User>()
            .Include(u => u.Role)
            .Where(u => u.OrganizationId == organizationId && u.IsActive);

        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search.FTS) ||
                u.LastName.Contains(search.FTS) ||
                u.Username.Contains(search.FTS) ||
                u.Email.Contains(search.FTS));
        }

        query = query.OrderBy(u => u.Role.Name)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.FirstName);

        var totalCount = await query.CountAsync(cancellationToken);

        if (search?.Page.HasValue == true && search?.PageSize.HasValue == true)
        {
            query = query.Skip(search.Page.Value * search.PageSize.Value)
                         .Take(search.PageSize.Value);
        }

        var users = await query.ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>
        {
            Items = Mapper.Map<List<UserResponse>>(users),
            TotalCount = totalCount,
            Page = search?.Page,
            PageSize = search?.PageSize
        };
    }

    protected override OrganizationResponse MapToResponse(Organization entity)
    {
        var response = Mapper.Map<OrganizationResponse>(entity);
        return response;
    }
}
