using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Repositories;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace eTicketing.Identity.Business.Organizations;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IdentityOptions _options;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<IdentityOptions> options)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
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

    public async Task<Result<OrganizationLogo>> GetLogoAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        return organization?.LogoData is null || organization.LogoContentType is null
            ? Result<OrganizationLogo>.Failure(Error.NotFound("organization.logo_not_found", "Organizacija nema logo."))
            : Result<OrganizationLogo>.Success(new OrganizationLogo(organization.LogoData, organization.LogoContentType));
    }

    public async Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.AdminEmail, request.AdminUsername, ct))
        {
            return Result<OrganizationResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var organization = request.Adapt<Organization>();
        if (request.Logo is not null)
        {
            (organization.LogoData, organization.LogoContentType) = await ReadLogoAsync(request.Logo, ct);
        }
        await _organizationRepository.AddAsync(organization, ct);

        var adminUser = request.Adapt<User>();
        adminUser.Organization = organization;
        (adminUser.PasswordHash, adminUser.PasswordSalt) = PasswordHasher.Hash(request.AdminPassword);

        await _userRepository.AddAsync(adminUser, ct);

        // Jedan SaveChangesAsync poziv — organizacija i prvi organizator se upisuju u istoj transakciji.
        await _unitOfWork.SaveChangesAsync(ct);

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
        // CreatedAt/Users untouched.
        request.Adapt(organization);

        if (request.Logo is not null)
        {
            (organization.LogoData, organization.LogoContentType) = await ReadLogoAsync(request.Logo, ct);
        }
        else if (request.RemoveLogo)
        {
            organization.LogoData = null;
            organization.LogoContentType = null;
        }
        // else: neither a replacement nor a removal was requested — keep the existing logo.

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

        _organizationRepository.Remove(organization);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, OrganizationUserQuery query, CancellationToken ct = default)
    {
        var paged = await _userRepository.SearchByOrganizationAsync(organizationId, query, query.Role, ct);
        return Result<PagedResult<UserResponse>>.Success(paged.Adapt<PagedResult<UserResponse>>());
    }

    public async Task<Result<UserResponse>> AddUserAsync(Guid organizationId, AddOrganizationUserRequest request, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (organization is null)
            return Result<UserResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var user = request.Adapt<User>();
        user.OrganizationId = organizationId;
        (user.PasswordHash, user.PasswordSalt) = PasswordHasher.Hash(request.Password);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result> RemoveUserAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || user.OrganizationId != organizationId)
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen u ovoj organizaciji."));

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static async Task<(byte[] Data, string ContentType)> ReadLogoAsync(IFormFile logo, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await logo.CopyToAsync(ms, ct);
        return (ms.ToArray(), logo.ContentType);
    }

    // LogoUrl is derived (not a stored column), so it's filled in here rather than via Mapster
    // — same reasoning as CategoryService.ToResponse in the Catalog service.
    private OrganizationResponse ToResponse(Organization organization) =>
        organization.Adapt<OrganizationResponse>() with { LogoUrl = BuildLogoUrl(organization) };

    private string? BuildLogoUrl(Organization organization) =>
        organization.LogoData is null ? null : $"{_options.PublicBaseUrl.TrimEnd('/')}/organizations/{organization.Id}/logo";
}
