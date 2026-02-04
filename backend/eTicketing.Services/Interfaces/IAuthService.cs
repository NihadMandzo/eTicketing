using eTicketing.Model.Requests;
using eTicketing.Model.Responses;

namespace eTicketing.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<bool> VerifyEmailAsync(VerifyEmailRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<UserResponse?> GetMeAsync(int userId);
    Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
}
