using eTicketing.Contracts.Events;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Contracts.Security;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using Mapster;

namespace eTicketing.Identity.Business.Admins;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;

    public AdminService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
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

    public async Task<Result> DeleteAsync(Guid id, DeleteAdminRequest request, CancellationToken ct = default)
    {
        // Needs Organization loaded (unlike the old GetByIdAsync) so the deletion notification —
        // when the target is an OrganizationAdmin — can carry the organization's name.
        var user = await _userRepository.GetByIdWithOrganizationAsync(id, ct);
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

        string? reason = null;
        string? recipientEmail = null;
        string? organizationName = null;
        Guid? organizationId = null;

        // Reason + a notification recipient are only meaningful — and only required — when the
        // target is an OrganizationAdmin (the case this feature is actually for). Any other
        // staff role (Admin) has no organization to notify.
        if (user.Role == RoleType.OrganizationAdmin)
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.RecipientEmail))
            {
                return Result.Failure(Error.Validation(
                    "admin.delete_reason_required",
                    "Razlog brisanja i email organizacije su obavezni kada brišete administratora organizacije."));
            }

            reason = request.Reason;
            recipientEmail = request.RecipientEmail;
            organizationName = user.Organization?.Name;
            organizationId = user.OrganizationId;
        }

        var deletedFullName = $"{user.FirstName} {user.LastName}";

        _userRepository.Remove(user);
        await _unitOfWork.SaveChangesAsync(ct);

        if (organizationId is not null)
        {
            await _eventPublisher.PublishAsync(
                EventNames.OrganizationAdminDeleted,
                new OrganizationAdminDeletedNotification(
                    organizationId.Value, organizationName ?? string.Empty, deletedFullName, recipientEmail!, reason!),
                ct);
        }

        return Result.Success();
    }

    public async Task<Result> SetPasswordAsync(Guid id, SetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);

        // Deliberately excludes both User (buyers self-service via ForgotPassword) and
        // SuperAdmin (per the confirmed scope — SuperAdmin can only set the password of Admin/
        // OrganizationSuperAdmin/OrganizationAdmin accounts, not a peer SuperAdmin's).
        if (user is null || user.Role is RoleType.User or RoleType.SuperAdmin)
            return Result.Failure(Error.NotFound("admin.not_found", "Korisnik nije pronađen."));

        var (hash, salt) = PasswordHasher.Hash(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;

        // The SuperAdmin-assigned password immediately invalidates any session the account
        // already had — same reasoning as every other password-changing path in this service.
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(
            EventNames.AdminPasswordChanged,
            new AdminPasswordChangedNotification(user.Id, user.Email, user.FirstName),
            ct);

        return Result.Success();
    }
}
