using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations.Validators;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using eTicketing.Shared.Storage;
using Mapster;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public class OrganizationService : IOrganizationService
{
    private const string ContainerName = "organization-logos";

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IEventPublisher _eventPublisher;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorageService,
        IEventPublisher eventPublisher)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _blobStorageService = blobStorageService;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default)
    {
        var paged = await _organizationRepository.SearchAsync(query, query.OrganizationIds, ct);
        var items = paged.Items.Select(ToResponse).ToList();
        return Result<PagedResult<OrganizationResponse>>.Success(new PagedResult<OrganizationResponse>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdWithUsersAsync(id, ct);

        return organization is null
            ? Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."))
            : Result<OrganizationResponse>.Success(ToResponse(organization));
    }

    public async Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.AdminEmail, request.AdminUsername, ct))
        {
            return Result<OrganizationResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var organization = request.Adapt<Organization>();
        await _organizationRepository.AddAsync(organization, ct);

        var adminUser = request.Adapt<User>();
        adminUser.Organization = organization;
        (adminUser.PasswordHash, adminUser.PasswordSalt) = PasswordHasher.Hash(request.AdminPassword);

        await _userRepository.AddAsync(adminUser, ct);

        // Jedan SaveChangesAsync poziv — organizacija i prvi organizator se upisuju u istoj transakciji.
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(
            EventNames.OrganizationCreated,
            new OrganizationCreatedNotification(organization.Id, organization.Name, request.NotificationEmail),
            ct);

        // EF's change-tracker fixup already put adminUser into organization.Users once both
        // entities were tracked (set via the Organization nav property above), so the mapped
        // UserCount comes out as 1 without a separate query.
        return Result<OrganizationResponse>.Success(ToResponse(organization));
    }

    public async Task<Result<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        if (organization is null)
            return Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        // In-place update — Mapster maps matching members (Name/Description/Address/
        // PhoneNumber/Email/Website/IsActive) onto the already-tracked entity, leaving Id/
        // CreatedAt/Users/LogoBlobName untouched (logos are managed exclusively through the
        // dedicated logo endpoints).
        request.Adapt(organization);
        await _unitOfWork.SaveChangesAsync(ct);

        var userCount = await _userRepository.CountByOrganizationAsync(id, ct);

        // organization.Users isn't loaded here (GetByIdAsync, unlike GetByIdWithUsersAsync,
        // doesn't Include it), so the mapped UserCount would come out as 0 — override it with
        // the freshly counted value instead of paying for an Include just for this one field.
        return Result<OrganizationResponse>.Success(ToResponse(organization) with { UserCount = userCount });
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        if (organization is null)
            return Result.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        // Hard-deleting an organization takes its users with it — every organization has at
        // least one (the admin created alongside it, see CreateAsync), and there's no soft
        // delete anywhere in this app to fall back on. The FK (Users.OrganizationId ->
        // Organizations.Id) is deliberately Restrict, not a DB-level cascade — deleted here,
        // explicitly, in the same SaveChangesAsync call as the organization itself, so it's one
        // atomic operation and stays testable without depending on the DB's own cascade
        // behavior. Events/tickets referencing this organization live in other services' own
        // databases (no cross-service FK) and are deliberately left untouched.
        var users = await _userRepository.GetAllByOrganizationAsync(id, ct);
        foreach (var user in users)
        {
            _userRepository.Remove(user);
        }

        _organizationRepository.Remove(organization);
        await _unitOfWork.SaveChangesAsync(ct);

        // DB delete first: if SaveChangesAsync above throws, the blob is left untouched rather
        // than ending up orphaned while the organization row is still alive and pointing at it.
        // If the blob delete below throws instead (genuine Azure outage) — after the DB commit
        // already succeeded — the organization is gone but the blob lingers; that orphaned-blob
        // state is acceptable and recoverable (it's simply never referenced again), unlike the
        // reverse.
        if (organization.LogoBlobName is not null)
        {
            await _blobStorageService.DeleteAsync(ContainerName, organization.LogoBlobName, ct);
        }

        return Result.Success();
    }

    public async Task<Result<OrganizationResponse>> UploadLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        if (organization is null)
            return Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        if (organization.LogoBlobName is not null)
        {
            return Result<OrganizationResponse>.Failure(Error.Conflict(
                "organization.logo_already_exists",
                "Organizacija već ima logo — koristite izmjenu da ga zamijenite."));
        }

        var extension = await OrganizationLogoValidation.DetectExtensionAsync(logo, ct);
        var blobName = BlobNaming.BuildBlobName(organization.Id, organization.Name, extension);
        await _blobStorageService.UploadAsync(ContainerName, blobName, logo.OpenReadStream(), ContentTypeFor(extension), ct);
        organization.LogoBlobName = blobName;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<OrganizationResponse>.Success(ToResponse(organization));
    }

    public async Task<Result<OrganizationResponse>> ReplaceLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        if (organization is null)
            return Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        if (organization.LogoBlobName is null)
        {
            return Result<OrganizationResponse>.Failure(Error.NotFound(
                "organization.logo_not_found",
                "Organizacija još nema logo — koristite kreiranje da ga dodate."));
        }

        // Re-uploads to the SAME blob key (overwrite) — the key was fixed at first-upload time
        // and deliberately doesn't track later Name changes (see Organization.LogoBlobName). A
        // replacement can switch PNG<->JPEG bytes but keeps the original extension in the key —
        // acceptable: the blob's Content-Type header (set from the new file) is what actually
        // governs how it's served/rendered, not the key's extension.
        var extension = await OrganizationLogoValidation.DetectExtensionAsync(logo, ct);
        await _blobStorageService.UploadAsync(ContainerName, organization.LogoBlobName, logo.OpenReadStream(), ContentTypeFor(extension), ct);

        return Result<OrganizationResponse>.Success(ToResponse(organization));
    }

    public async Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, OrganizationUserQuery query, ClaimsPrincipal caller, CancellationToken ct = default)
    {
        // Read-only — any member of the organization (either org role) may view their own org's
        // staff list, not just an OrganizationSuperAdmin. Platform staff can view any org's.
        if (!caller.IsPlatformStaff() && caller.GetOrganizationId() != organizationId)
            return Result<PagedResult<UserResponse>>.Failure(Error.Unauthorized(
                "organization.forbidden", "Nemate ovlaštenje za pregled korisnika ove organizacije."));

        var paged = await _userRepository.SearchByOrganizationAsync(organizationId, query, query.Role, ct);
        return Result<PagedResult<UserResponse>>.Success(paged.Adapt<PagedResult<UserResponse>>());
    }

    public async Task<Result<UserResponse>> AddUserAsync(Guid organizationId, AddOrganizationUserRequest request, ClaimsPrincipal caller, CancellationToken ct = default)
    {
        var authError = AuthorizeOrgUserManagement(caller, organizationId);
        if (authError is not null)
            return Result<UserResponse>.Failure(authError);

        // A self-servicing OrganizationSuperAdmin may only add OrganizationAdmin accounts to
        // their own org — this alone rules out the self-service path ever creating a second
        // OrganizationSuperAdmin. Platform staff aren't restricted here, so the uniqueness check
        // below still guards their path too.
        if (!caller.IsPlatformStaff() && request.Role != RoleType.OrganizationAdmin)
        {
            return Result<UserResponse>.Failure(Error.Validation(
                "organization.role_not_allowed", "Možete dodati samo korisnike sa ulogom OrganizationAdmin."));
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (organization is null)
            return Result<UserResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        // Every organization must have exactly one OrganizationSuperAdmin — reject a second one
        // regardless of who's adding it.
        if (request.Role == RoleType.OrganizationSuperAdmin
            && await _userRepository.ExistsByOrganizationAndRoleAsync(organizationId, RoleType.OrganizationSuperAdmin, ct))
        {
            return Result<UserResponse>.Failure(Error.Conflict(
                "organization.super_admin_already_exists", "Organizacija već ima Super Administratora."));
        }

        var user = request.Adapt<User>();
        user.OrganizationId = organizationId;
        (user.PasswordHash, user.PasswordSalt) = PasswordHasher.Hash(request.Password);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result<UserResponse>> UpdateUserAsync(Guid organizationId, Guid userId, UpdateOrganizationUserRequest request, ClaimsPrincipal caller, CancellationToken ct = default)
    {
        var authError = AuthorizeOrgUserManagement(caller, organizationId);
        if (authError is not null)
            return Result<UserResponse>.Failure(authError);

        var user = await _userRepository.GetByIdWithOrganizationAsync(userId, ct);
        if (user is null || user.OrganizationId != organizationId)
            return Result<UserResponse>.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen u ovoj organizaciji."));

        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, user.Id, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        request.Adapt(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result> RemoveUserAsync(Guid organizationId, Guid userId, ClaimsPrincipal caller, CancellationToken ct = default)
    {
        var authError = AuthorizeOrgUserManagement(caller, organizationId);
        if (authError is not null)
            return Result.Failure(authError);

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || user.OrganizationId != organizationId)
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen u ovoj organizaciji."));

        // Same invariant as AdminService.DeleteAsync: exactly one OrganizationSuperAdmin per
        // organization, always — no transfer-ownership flow exists, so this is blocked
        // unconditionally, even for platform staff.
        if (user.Role == RoleType.OrganizationSuperAdmin)
        {
            return Result.Failure(Error.Conflict(
                "organization.super_admin_required",
                "Ne možete obrisati Super Administratora organizacije."));
        }

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Shared ownership/role check for AddUserAsync/UpdateUserAsync/RemoveUserAsync:
    /// platform staff (SuperAdmin/Admin) may manage any organization's users; anyone else must be
    /// that exact organization's OrganizationSuperAdmin — an OrganizationAdmin cannot manage
    /// their own org's other users, and nobody can reach into a different organization.</summary>
    private static Error? AuthorizeOrgUserManagement(ClaimsPrincipal caller, Guid organizationId)
    {
        if (caller.IsPlatformStaff())
            return null;

        if (caller.GetRole() != nameof(RoleType.OrganizationSuperAdmin))
            return Error.Unauthorized("organization.forbidden", "Nemate ovlaštenje za upravljanje korisnicima organizacije.");

        if (caller.GetOrganizationId() != organizationId)
            return Error.Unauthorized("organization.forbidden", "Nemate ovlaštenje za upravljanje korisnicima ove organizacije.");

        return null;
    }

    private static string ContentTypeFor(string extension) => extension == "png" ? "image/png" : "image/jpeg";

    // LogoUrl is derived (not a stored column), so it's filled in here rather than via Mapster
    // — same reasoning as CategoryService.ToResponse in the Catalog service.
    private OrganizationResponse ToResponse(Organization organization) =>
        organization.Adapt<OrganizationResponse>() with { LogoUrl = BuildLogoUrl(organization) };

    private string? BuildLogoUrl(Organization organization) =>
        organization.LogoBlobName is null ? null : _blobStorageService.GetPublicUrl(ContainerName, organization.LogoBlobName);
}
