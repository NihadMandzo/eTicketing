using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using Microsoft.EntityFrameworkCore;

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
        var q = _organizationRepository.Query()
            .Where(o => string.IsNullOrEmpty(query.FTS) || o.Name.Contains(query.FTS))
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationResponse(
                o.Id, o.Name, o.Description, o.Address, o.PhoneNumber, o.Email, o.Website,
                o.LogoUrl, o.IsActive, o.Users.Count, o.CreatedAt));

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return Result<PagedResult<OrganizationResponse>>.Success(paged);
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var org = await _organizationRepository.Query()
            .Where(o => o.Id == id)
            .Select(o => new OrganizationResponse(
                o.Id, o.Name, o.Description, o.Address, o.PhoneNumber, o.Email, o.Website,
                o.LogoUrl, o.IsActive, o.Users.Count, o.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return org is null
            ? Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."))
            : Result<OrganizationResponse>.Success(org);
    }

    public async Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.AdminEmail, request.AdminUsername, ct))
        {
            return Result<OrganizationResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var organization = new Organization
        {
            Name = request.Name,
            Description = request.Description,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Website = request.Website,
            IsActive = true
        };

        await _organizationRepository.AddAsync(organization, ct);

        var (hash, salt) = PasswordHasher.Hash(request.AdminPassword);
        var adminUser = new User
        {
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            Email = request.AdminEmail,
            Username = request.AdminUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = request.AdminRole,
            Organization = organization,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = true
        };

        await _userRepository.AddAsync(adminUser, ct);

        // Jedan SaveChangesAsync poziv — organizacija i prvi organizator se upisuju u istoj transakciji.
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            organization.Id, organization.Name, organization.Description, organization.Address,
            organization.PhoneNumber, organization.Email, organization.Website, organization.LogoUrl,
            organization.IsActive, 1, organization.CreatedAt));
    }

    public async Task<Result<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, ct);
        if (organization is null)
            return Result<OrganizationResponse>.Failure(Error.NotFound("organization.not_found", "Organizacija nije pronađena."));

        organization.Name = request.Name;
        organization.Description = request.Description;
        organization.Address = request.Address;
        organization.PhoneNumber = request.PhoneNumber;
        organization.Email = request.Email;
        organization.Website = request.Website;
        organization.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(ct);

        var userCount = await _userRepository.Query().CountAsync(u => u.OrganizationId == id, ct);

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            organization.Id, organization.Name, organization.Description, organization.Address,
            organization.PhoneNumber, organization.Email, organization.Website, organization.LogoUrl,
            organization.IsActive, userCount, organization.CreatedAt));
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
        var q = _userRepository.Query()
            .Where(u => u.OrganizationId == organizationId)
            .OrderBy(u => u.LastName)
            .Select(u => new UserResponse(
                u.Id, u.FirstName, u.LastName, u.Email, u.Username, u.PhoneNumber, u.Role.ToString(),
                u.OrganizationId, u.IsActive, u.IsEmailVerified, u.IsFirstLogin, u.CreatedAt, u.LastLoginAt));

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return Result<PagedResult<UserResponse>>.Success(paged);
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

        var (hash, salt) = PasswordHasher.Hash(request.Password);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Username = request.Username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = request.Role,
            OrganizationId = organizationId,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = true
        };

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserResponse>.Success(new UserResponse(
            user.Id, user.FirstName, user.LastName, user.Email, user.Username, user.PhoneNumber,
            user.Role.ToString(), user.OrganizationId, user.IsActive, user.IsEmailVerified, user.IsFirstLogin,
            user.CreatedAt, user.LastLoginAt));
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
