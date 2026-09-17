using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Identity.Api.Infrastructure;
using eTicketing.Identity.Business.Auth;
using eTicketing.Contracts.Security;
using eTicketing.Identity.Business.Security;
using eTicketing.Shared.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace eTicketing.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register).AllowAnonymous().WithValidation<RegisterRequest>()
            .RequireRateLimiting(AuthRateLimitPolicies.AuthAttempts);
        group.MapPost("/login", Login).AllowAnonymous().WithValidation<LoginRequest>()
            .RequireRateLimiting(AuthRateLimitPolicies.AuthAttempts);
        group.MapPost("/refresh", Refresh).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/change-password", ChangePassword).RequireAuthorization().WithValidation<ChangePasswordRequest>();
        group.MapPut("/update-user", UpdateUser).RequireAuthorization().WithValidation<UpdateUserRequest>();
        group.MapPost("/verify-email", VerifyEmail).RequireAuthorization().WithValidation<VerifyEmailRequest>();
        // Both send an outbound email with no other cooldown — rate-limited (see
        // IdentityServiceCollectionExtensions' "email-sending" policy) so a script can't flood a
        // victim's inbox or burn through the platform's email-send quota.
        group.MapPost("/resend-verification-email", ResendVerificationEmail).RequireAuthorization().RequireRateLimiting(AuthRateLimitPolicies.EmailSending);
        group.MapPost("/forgot-password", ForgotPassword).AllowAnonymous().WithValidation<ForgotPasswordRequest>().RequireRateLimiting(AuthRateLimitPolicies.EmailSending);
        group.MapPost("/reset-password", ResetPassword).AllowAnonymous().WithValidation<ResetPasswordRequest>();
        group.MapPost("/set-new-password", SetNewPassword).RequireAuthorization().WithValidation<SetNewPasswordRequest>();
    }

    private static async Task<IResult> Register(
        RegisterRequest request, IAuthService service, IAuthCookieService cookies, HttpContext http, CancellationToken ct)
    {
        var result = await service.RegisterAsync(request, ct);
        if (result.IsFailure) return result.ToHttpResult();

        cookies.WriteAuthCookies(http, result.Value!);
        return Results.Json(new LoginResponse(result.Value!.User), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(
        LoginRequest request, IAuthService service, IAuthCookieService cookies, HttpContext http, CancellationToken ct)
    {
        var result = await service.LoginAsync(request, ct);
        if (result.IsFailure) return result.ToHttpResult();

        cookies.WriteAuthCookies(http, result.Value!);
        return Results.Ok(new LoginResponse(result.Value!.User));
    }

    private static async Task<IResult> Refresh(
        IAuthService service, IAuthCookieService cookies, HttpContext http, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await service.RefreshAsync(rawRefreshToken, ct);
        if (result.IsFailure)
        {
            cookies.ClearAuthCookies(http);
            return result.ToHttpResult();
        }

        cookies.WriteAuthCookies(http, result.Value!);
        return Results.Ok(new LoginResponse(result.Value!.User));
    }

    private static async Task<IResult> Logout(IAuthService service, IAuthCookieService cookies, HttpContext http, CancellationToken ct)
    {
        if (http.Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
        {
            await service.LogoutAsync(rawRefreshToken, ct);
        }

        cookies.ClearAuthCookies(http);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(IAuthService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMeAsync(http.User.GetUserId(), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request, IAuthService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.ChangePasswordAsync(http.User.GetUserId(), request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateUser(
        UpdateUserRequest request, IAuthService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateUserAsync(http.User.GetUserId(), request, ct);
        return result.ToHttpResult();
    }

    // Deliberately no UserId/target-identifier field on VerifyEmailRequest — the target is
    // always the authenticated caller, read from their own token claim. This is what makes it
    // structurally impossible for a user to verify (or resend a verification for) anyone else's
    // email, not just a convention.
    private static async Task<IResult> VerifyEmail(
        VerifyEmailRequest request, IAuthService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.VerifyEmailAsync(http.User.GetUserId(), request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResendVerificationEmail(IAuthService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.ResendVerificationEmailAsync(http.User.GetUserId(), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request, IAuthService service, CancellationToken ct)
    {
        var result = await service.ForgotPasswordAsync(request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request, IAuthService service, CancellationToken ct)
    {
        var result = await service.ResetPasswordAsync(request, ct);
        return result.ToHttpResult();
    }

    // Closes the SuperAdmin-forced-password-change loop: on success, clears the session cookies
    // so the caller must re-authenticate with the password they just chose themselves, rather
    // than silently continuing under the SuperAdmin-assigned one.
    private static async Task<IResult> SetNewPassword(
        SetNewPasswordRequest request, IAuthService service, IAuthCookieService cookies, HttpContext http, CancellationToken ct)
    {
        var result = await service.SetNewPasswordAsync(http.User.GetUserId(), request, ct);
        if (result.IsFailure) return result.ToHttpResult();

        cookies.ClearAuthCookies(http);
        return Results.NoContent();
    }
}
