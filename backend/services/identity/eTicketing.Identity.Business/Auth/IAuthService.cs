using eTicketing.Contracts.Results;

namespace eTicketing.Identity.Business.Auth;

public interface IAuthService
{
    Task<Result<LoginResult>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<LoginResult>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<LoginResult>> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);
    Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default);
}
