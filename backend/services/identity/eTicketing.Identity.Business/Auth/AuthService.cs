using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEventPublisher _eventPublisher;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IEventPublisher eventPublisher,
        IOptions<JwtOptions> jwtOptions)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
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

        await _userRepository.AddAsync(user, ct);

        var loginResult = await IssueTokensAsync(user, ct);

        var verificationCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await _eventPublisher.PublishAsync(
            EventNames.VerificationEmailRequested,
            new VerificationEmailRequested(user.Id, user.Email, user.FirstName, verificationCode),
            ct);

        return Result<LoginResult>.Success(loginResult);
    }

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
