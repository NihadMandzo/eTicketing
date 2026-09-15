using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Security;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using Mapster;
using Microsoft.Extensions.Options;

namespace eTicketing.Identity.Business.Auth;

public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEventPublisher _eventPublisher;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IEventPublisher eventPublisher,
        IOptions<JwtOptions> jwtOptions)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _eventPublisher = eventPublisher;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<LoginResult>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsByEmailOrUsernameAsync(request.Email, request.Username, ct))
        {
            return Result<LoginResult>.Failure(
                Error.Conflict("user.already_exists", "Korisnik sa ovim emailom ili korisničkim imenom već postoji."));
        }

        var user = request.Adapt<User>();
        (user.PasswordHash, user.PasswordSalt) = PasswordHasher.Hash(request.Password);

        var verificationCode = GenerateVerificationCode();
        user.EmailVerificationCode = verificationCode;
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddHours(24);

        await _userRepository.AddAsync(user, ct);

        // IssueTokensAsync does the one SaveChangesAsync for this method — the verification
        // code set above is persisted in that same call, no extra DB round trip.
        var loginResult = await IssueTokensAsync(user, ct);

        await _eventPublisher.PublishAsync(
            EventNames.VerificationEmailRequested,
            new VerificationEmailRequested(user.Id, user.Email, user.FirstName, verificationCode),
            ct);

        return Result<LoginResult>.Success(loginResult);
    }

    private static string GenerateVerificationCode() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    public async Task<Result<LoginResult>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.EmailOrUsername, ct)
            ?? await _userRepository.GetByUsernameAsync(request.EmailOrUsername, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.invalid_credentials", "Pogrešan email/korisničko ime ili lozinka."));
        }

        if (!user.IsActive)
        {
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.inactive_account", "Nalog je deaktiviran."));
        }

        user.LastLoginAt = DateTime.UtcNow;

        var loginResult = await IssueTokensAsync(user, ct);
        return Result<LoginResult>.Success(loginResult);
    }

    public async Task<Result<LoginResult>> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenGenerator.Hash(rawRefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(hash, ct);

        if (stored is null)
        {
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.invalid_refresh_token", "Sesija nije validna. Prijavite se ponovo."));
        }

        if (stored.RevokedAt is not null)
        {
            // The refresh token was already rotated away, yet it's being presented again —
            // this is reuse (stolen/replayed token). Revoke the whole family and force a full re-login.
            await _refreshTokenRepository.RevokeAllActiveForUserAsync(stored.UserId, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.refresh_reuse_detected", "Sumnjiva aktivnost detektovana. Prijavite se ponovo."));
        }

        if (stored.ExpiresAt < DateTime.UtcNow)
        {
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.refresh_expired", "Sesija je istekla. Prijavite se ponovo."));
        }

        var user = stored.User;

        if (!user.IsActive)
        {
            // A deactivated user must not be able to keep minting access tokens off an
            // already-issued refresh token — deactivation has to end their session immediately,
            // the same way LoginAsync already refuses them.
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.inactive_account", "Nalog je deaktiviran."));
        }

        var now = DateTime.UtcNow;
        var newRawToken = RefreshTokenGenerator.GenerateRaw();
        var newHash = RefreshTokenGenerator.Hash(newRawToken);

        // Single conditional UPDATE (TokenHash = hash AND RevokedAt IS NULL) — the atomic
        // compare-and-swap that stops two concurrent requests from both reading this token as
        // still-active and each rotating it into a valid successor. Only the request that
        // actually flips RevokedAt wins; the loser must fail rather than issue a second token.
        var revoked = await _refreshTokenRepository.TryRevokeAsync(hash, newHash, now, ct);
        if (!revoked)
        {
            return Result<LoginResult>.Failure(
                Error.Unauthorized("auth.invalid_refresh_token", "Sesija nije validna. Prijavite se ponovo."));
        }

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays)
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _tokenGenerator.GenerateAccessToken(user);
        return Result<LoginResult>.Success(new LoginResult(user.Adapt<UserResponse>(), accessToken, newRawToken));
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenGenerator.Hash(rawRefreshToken);
        await _refreshTokenRepository.RevokeAsync(hash, DateTime.UtcNow, ct);
    }

    public async Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithOrganizationAsync(userId, ct);
        return user is null
            ? Result<UserResponse>.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."))
            : Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."));
        }

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return Result.Failure(Error.Validation("auth.wrong_current_password", "Trenutna lozinka nije tačna."));
        }

        var (hash, salt) = PasswordHasher.Hash(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        // A password change must invalidate any refresh token an attacker (or a previous,
        // now-untrusted device) already holds — otherwise it can keep minting access tokens
        // long after the password was rotated.
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<UserResponse>> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithOrganizationAsync(userId, ct);
        if (user is null)
        {
            return Result<UserResponse>.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."));
        }

        if (!string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase)
            && await _userRepository.ExistsByUsernameAsync(request.Username, user.Id, ct))
        {
            return Result<UserResponse>.Failure(
                Error.Conflict("user.already_exists", "Korisničko ime je zauzeto."));
        }

        // In-place update — Mapster maps matching members (FirstName/LastName/Username/
        // PhoneNumber) onto the already-tracked entity, leaving everything else untouched.
        request.Adapt(user);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<UserResponse>.Success(user.Adapt<UserResponse>());
    }

    public async Task<Result> VerifyEmailAsync(Guid userId, VerifyEmailRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."));
        }

        if (user.IsEmailVerified)
        {
            return Result.Failure(Error.Conflict("auth.email_already_verified", "Email adresa je već potvrđena."));
        }

        if (string.IsNullOrEmpty(user.EmailVerificationCode) || user.EmailVerificationCodeExpiresAt is null
            || user.EmailVerificationCodeExpiresAt < DateTime.UtcNow)
        {
            return Result.Failure(Error.Validation(
                "auth.verification_code_expired", "Verifikacioni kod je istekao. Zatražite novi."));
        }

        if (!string.Equals(user.EmailVerificationCode, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "auth.invalid_verification_code", "Verifikacioni kod nije ispravan."));
        }

        // Cleared, not just flagged verified — this is what makes the code single-use: a repeat
        // VerifyEmailAsync call with the same code now hits the expired/missing branch above.
        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ResendVerificationEmailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."));
        }

        if (user.IsEmailVerified)
        {
            return Result.Failure(Error.Conflict("auth.email_already_verified", "Email adresa je već potvrđena."));
        }

        // Overwriting the code/expiry here is what makes the *old* code stop working — a stale
        // copy of this email sitting in an inbox can no longer verify anything.
        var verificationCode = GenerateVerificationCode();
        user.EmailVerificationCode = verificationCode;
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddHours(24);

        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(
            EventNames.VerificationEmailRequested,
            new VerificationEmailRequested(user.Id, user.Email, user.FirstName, verificationCode),
            ct);

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);

        // Unknown email: succeed anyway, without sending anything — this is deliberate
        // anti-enumeration for the "no such account" case (see the staff-role branch below for
        // the one case where this plan intentionally trades that away).
        if (user is null)
        {
            return Result.Success();
        }

        // Only buyers (User role) may self-service a password reset — staff/organization
        // accounts can only have their password set directly by SuperAdmin (see
        // AdminService.SetPasswordAsync). Unlike the unknown-email case above, this is a named,
        // visible error on purpose: a staff member deserves to know why nothing arrived rather
        // than staring at a silent no-op forever.
        if (user.Role != RoleType.User)
        {
            return Result.Failure(Error.Validation(
                "auth.forgot_password_staff_not_allowed",
                "Za ovaj tip naloga lozinku može promijeniti samo SuperAdmin. Kontaktirajte administratora platforme."));
        }

        // Any older still-valid reset link becomes unusable the moment a new one is requested —
        // otherwise both links would work simultaneously, which is surprising and marginally
        // less secure (a leaked earlier link would stay live).
        await _passwordResetTokenRepository.InvalidateAllActiveForUserAsync(user.Id, ct);

        var rawToken = RefreshTokenGenerator.GenerateRaw();
        await _passwordResetTokenRepository.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(
            EventNames.PasswordResetRequested,
            new PasswordResetRequested(user.Id, user.Email, user.FirstName, rawToken),
            ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var tokenHash = RefreshTokenGenerator.Hash(request.Token);
        var storedToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        if (storedToken is null || !storedToken.IsValid)
        {
            return Result.Failure(Error.Unauthorized(
                "auth.invalid_reset_token", "Link za resetovanje lozinke nije validan ili je istekao."));
        }

        var user = storedToken.User;
        var (hash, salt) = PasswordHasher.Hash(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        // Single-use: marking it here means a second ResetPasswordAsync call with the same raw
        // token fails the IsValid check above instead of silently succeeding again.
        storedToken.UsedAt = DateTime.UtcNow;

        // Same reasoning as ChangePasswordAsync — a password reset must invalidate any refresh
        // token already in play, attacker-held or not.
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetNewPasswordAsync(Guid userId, SetNewPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "Korisnik nije pronađen."));
        }

        // This flow exists only to close the SuperAdmin-forced-password-change loop (see the
        // class doc on SetNewPasswordRequest) — it deliberately skips the current-password check
        // ChangePasswordAsync enforces. Without this guard, any authenticated caller could set an
        // arbitrary new password with no proof of the old one, a full account-takeover path for
        // anyone holding a live session (stolen cookie, unlocked device, etc.).
        if (!user.MustChangePassword)
        {
            return Result.Failure(Error.Unauthorized(
                "auth.password_change_not_required", "Promjena lozinke nije potrebna za ovaj nalog."));
        }

        var (hash, salt) = PasswordHasher.Hash(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;

        // The caller is currently authenticated off the SuperAdmin-assigned password — revoking
        // every active refresh token here (the endpoint also clears the auth cookies, see
        // AuthEndpoints.SetNewPassword) forces a clean re-login with the password they just chose
        // themselves, rather than silently continuing the old session.
        await _refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<LoginResult> IssueTokensAsync(User user, CancellationToken ct)
    {
        var rawRefreshToken = RefreshTokenGenerator.GenerateRaw();

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _tokenGenerator.GenerateAccessToken(user);
        return new LoginResult(user.Adapt<UserResponse>(), accessToken, rawRefreshToken);
    }
}
