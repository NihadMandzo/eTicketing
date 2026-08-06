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
        var paged = await _userRepository.SearchByRoleAsync(RoleType.Admin, query, ct);
        return Result<PagedResult<UserResponse>>.Success(paged.Adapt<PagedResult<UserResponse>>());
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null || user.Role != RoleType.Admin)
            return Result<UserResponse>.Failure(Error.NotFound("admin.not_found", "Admin nalog nije pronađen."));

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

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null || user.Role != RoleType.Admin)
            return Result.Failure(Error.NotFound("admin.not_found", "Admin nalog nije pronađen."));

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
