using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using Mapster;

namespace eTicketing.Identity.Business.Admins;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<UserResponse>>> GetAsync(AdminQuery query, CancellationToken ct = default)
    {
        var paged = await _userRepository.SearchStaffAsync(query.RoleFilters, query, ct);
        return Result<PagedResult<UserResponse>>.Success(paged.Adapt<PagedResult<UserResponse>>());
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithOrganizationAsync(id, ct);
        if (user is null || user.Role == RoleType.User)
            return Result<UserResponse>.Failure(Error.NotFound("admin.not_found", "Korisnik nije pronađen."));

        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var user = request.Adapt<User>();
        (user.PasswordHash, user.PasswordSalt) = PasswordHasher.Hash(request.Password);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateStaffUserRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithOrganizationAsync(id, ct);
        if (user is null || user.Role == RoleType.User)
            return Result<UserResponse>.Failure(Error.NotFound("admin.not_found", "Korisnik nije pronađen."));

        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, user.Id, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        // In-place update — Mapster maps matching members (FirstName/LastName/Email/Username/
        // PhoneNumber) onto the already-tracked entity, same as AuthService.UpdateUserAsync.
        request.Adapt(user);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null || user.Role == RoleType.User)
            return Result.Failure(Error.NotFound("admin.not_found", "Korisnik nije pronađen."));

        // Every organization must always have exactly one OrganizationSuperAdmin — there is no
        // transfer-ownership flow, so removing one here would leave the org permanently headless.
        // Blocked unconditionally, with no platform-staff override.
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
}
