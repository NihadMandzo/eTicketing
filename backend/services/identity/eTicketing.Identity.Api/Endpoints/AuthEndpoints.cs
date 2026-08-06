using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Shared.Auth;
using Microsoft.Extensions.Options;

namespace eTicketing.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register).AllowAnonymous().WithValidation<RegisterRequest>();
        group.MapPost("/login", Login).AllowAnonymous().WithValidation<LoginRequest>();
        group.MapPost("/refresh", Refresh).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/change-password", ChangePassword).RequireAuthorization().WithValidation<ChangePasswordRequest>();
        group.MapPut("/update-user", UpdateUser).RequireAuthorization().WithValidation<UpdateUserRequest>();
    }

    private static async Task<IResult> Register(
        RegisterRequest request, IAuthService service, HttpContext http,
        IHostEnvironment env, IOptions<JwtOptions> jwtOptions, CancellationToken ct)
    {
        var result = await service.RegisterAsync(request, ct);
        if (result.IsFailure) return result.ToHttpResult();

        WriteAuthCookies(http, env, result.Value!, jwtOptions.Value);
        return Results.Json(new LoginResponse(result.Value!.User), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(
        LoginRequest request, IAuthService service, HttpContext http,
        IHostEnvironment env, IOptions<JwtOptions> jwtOptions, CancellationToken ct)
    {
        var result = await service.LoginAsync(request, ct);
        if (result.IsFailure) return result.ToHttpResult();

        WriteAuthCookies(http, env, result.Value!, jwtOptions.Value);
        return Results.Ok(new LoginResponse(result.Value!.User));
    }

    private static async Task<IResult> Refresh(
        IAuthService service, HttpContext http, IHostEnvironment env, IOptions<JwtOptions> jwtOptions, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await service.RefreshAsync(rawRefreshToken, ct);
        if (result.IsFailure)
        {
            ClearAuthCookies(http, env);
            return result.ToHttpResult();
        }

        WriteAuthCookies(http, env, result.Value!, jwtOptions.Value);
        return Results.Ok(new LoginResponse(result.Value!.User));
    }

    private static async Task<IResult> Logout(IAuthService service, HttpContext http, IHostEnvironment env, CancellationToken ct)
    {
        if (http.Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
        {
            await service.LogoutAsync(rawRefreshToken, ct);
        }

        ClearAuthCookies(http, env);
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

    private static void WriteAuthCookies(HttpContext http, IHostEnvironment env, LoginResult result, JwtOptions jwtOptions)
    {
        http.Response.Cookies.Append(
            AuthCookieNames.AccessToken,
            result.AccessToken,
            AuthCookiePolicy.Build(env, DateTimeOffset.UtcNow.AddMinutes(jwtOptions.AccessTokenMinutes)));

        http.Response.Cookies.Append(
            AuthCookieNames.RefreshToken,
            result.RefreshToken,
            AuthCookiePolicy.Build(env, DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenDays)));
    }

    private static void ClearAuthCookies(HttpContext http, IHostEnvironment env)
    {
        var expiredOptions = AuthCookiePolicy.Build(env, DateTimeOffset.UtcNow.AddDays(-1));
        http.Response.Cookies.Delete(AuthCookieNames.AccessToken, expiredOptions);
        http.Response.Cookies.Delete(AuthCookieNames.RefreshToken, expiredOptions);
    }
}
