using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Repositories;
using Mapster;

namespace eTicketing.Identity.Business.Organizations;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default)
    {
        var paged = await _organizationRepository.SearchAsync(query, ct);
        return Result<PagedResult<OrganizationResponse>>.Success(paged.Adapt<PagedResult<OrganizationResponse>>());
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdWithUsersAsync(id, ct);

        return organization is null
            ? Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."))
            : Result<OrganizationResponse>.Success(organization.Adapt<OrganizationResponse>());
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

        // EF's change-tracker fixup already put adminUser into organization.Users once both
        // entities were tracked (set via the Organization nav property above), so the mapped
        // UserCount comes out as 1 without a separate query.
        return Result<OrganizationResponse>.Success(organization.Adapt<OrganizationResponse>());
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

        await _unitOfWork.SaveChangesAsync(ct);

        var userCount = await _userRepository.CountByOrganizationAsync(id, ct);

        // organization.Users isn't loaded here (GetByIdAsync, unlike GetByIdWithUsersAsync,
        // doesn't Include it), so the mapped UserCount would come out as 0 — override it with
        // the freshly counted value instead of paying for an Include just for this one field.
        return Result<OrganizationResponse>.Success(organization.Adapt<OrganizationResponse>() with { UserCount = userCount });
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

    public async Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, BaseSearchObject query, CancellationToken ct = default)
    {
        var paged = await _userRepository.SearchByOrganizationAsync(organizationId, query, ct);
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
}
