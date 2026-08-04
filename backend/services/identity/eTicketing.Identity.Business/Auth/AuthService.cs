using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;

namespace eTicketing.Identity.Business.Auth;

public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEventPublisher _eventPublisher;

    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IEventPublisher eventPublisher)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
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
            RoleId = (int)RoleType.User,
            IsActive = true,
            IsEmailVerified = false,
            IsFirstLogin = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var verificationCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await _eventPublisher.PublishAsync(
            EventNames.VerificationEmailRequested,
            new VerificationEmailRequested(user.Id, user.Email, user.FirstName, verificationCode),
            ct);

        return Result<UserResponse>.Success(ToResponse(user, "User"));
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.EmailOrUsername, ct)
            ?? await _userRepository.GetByUsernameAsync(request.EmailOrUsername, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Result<LoginResponse>.Failure(
                Error.Unauthorized("auth.invalid_credentials", "Pogrešan email/korisničko ime ili lozinka."));
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure(
                Error.Unauthorized("auth.inactive_account", "Nalog je deaktiviran."));
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        var token = _tokenGenerator.GenerateToken(user);
        return Result<LoginResponse>.Success(new LoginResponse(token, ToResponse(user, user.Role.Name)));
    }

    private static UserResponse ToResponse(User user, string roleName) => new(
        user.Id, user.FirstName, user.LastName, user.Email, user.Username, user.PhoneNumber,
        roleName, user.OrganizationId, user.IsActive, user.IsEmailVerified, user.IsFirstLogin,
        user.CreatedAt, user.LastLoginAt);
}
