using eTicketing.Api.Resources;
using eTicketing.Model.Requests;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace eTicketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Verify email with OTP code
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        await _authService.VerifyEmailAsync(request);
        return Ok(new { message = ErrorMessagesHr.EmailVerifiedSuccess });
    }

    /// <summary>
    /// Login with email/username and password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Request password reset - sends OTP code to email
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = ErrorMessagesHr.PasswordResetCodeSent });
    }

    /// <summary>
    /// Reset password using OTP code
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(new { message = ErrorMessagesHr.PasswordResetSuccess });
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue)
            return Unauthorized(new { message = ErrorMessagesHr.UserNotAuthenticated });
            
        await _authService.ChangePasswordAsync(userId.Value, request);
        return Ok(new { message = ErrorMessagesHr.PasswordChangedSuccess });
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue)
            return Unauthorized(new { message = ErrorMessagesHr.UserNotAuthenticated });
            
        var user = await _authService.GetMeAsync(userId.Value);
        
        if (user == null)
            return Unauthorized(new { message = ErrorMessagesHr.UserNotFound });

        return Ok(user);
    }
}
