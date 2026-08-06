using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;

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
        var q = _userRepository.Query()
            .Where(u => u.Role == RoleType.Admin)
            .Where(u => string.IsNullOrEmpty(query.FTS)
                || u.FirstName.Contains(query.FTS) || u.LastName.Contains(query.FTS) || u.Email.Contains(query.FTS))
            .OrderBy(u => u.LastName)
            .Select(u => new UserResponse(
                u.Id, u.FirstName, u.LastName, u.Email, u.Username, u.PhoneNumber, u.Role.ToString(),
                u.OrganizationId, u.IsActive, u.IsEmailVerified, u.IsFirstLogin, u.CreatedAt, u.LastLoginAt));

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return Result<PagedResult<UserResponse>>.Success(paged);
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null || user.Role != RoleType.Admin)
            return Result<UserResponse>.Failure(Error.NotFound("admin.not_found", "Admin nalog nije pronađen."));

        return Result<UserResponse>.Success(new UserResponse(
            user.Id, user.FirstName, user.LastName, user.Email, user.Username, user.PhoneNumber,
            user.Role.ToString(), user.OrganizationId, user.IsActive, user.IsEmailVerified, user.IsFirstLogin,
            user.CreatedAt, user.LastLoginAt));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
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
            PhoneNumber = request.PhoneNumber,
            Role = RoleType.Admin,
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
