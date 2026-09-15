using eTicketing.Contracts.Security;
using eTicketing.Identity.Business.Security;
using eTicketing.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace eTicketing.Identity.Business.Auth;

public interface IAuthCookieService
{
    /// <summary>Writes the httpOnly access + refresh token cookies for a freshly issued/rotated
    /// <see cref="LoginResult"/>.</summary>
    void WriteAuthCookies(HttpContext http, LoginResult result);

    /// <summary>Expires both auth cookies — used on logout and whenever a refresh attempt fails.</summary>
    void ClearAuthCookies(HttpContext http);
}

/// <summary>
/// Owns the httpOnly access/refresh cookie lifecycle for the auth flow: cookie names, policy
/// (see <see cref="AuthCookiePolicy"/>) and expiry math all live here, as one piece of business
/// logic, instead of being duplicated/inlined across AuthEndpoints' route handlers.
/// </summary>
public class AuthCookieService : IAuthCookieService
{
    private readonly IHostEnvironment _env;
    private readonly JwtOptions _jwtOptions;

    public AuthCookieService(IHostEnvironment env, IOptions<JwtOptions> jwtOptions)
    {
        _env = env;
        _jwtOptions = jwtOptions.Value;
    }

    public void WriteAuthCookies(HttpContext http, LoginResult result)
    {
        http.Response.Cookies.Append(
            AuthCookieNames.AccessToken,
            result.AccessToken,
            AuthCookiePolicy.Build(_env, DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes)));

        http.Response.Cookies.Append(
            AuthCookieNames.RefreshToken,
            result.RefreshToken,
            AuthCookiePolicy.Build(_env, DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)));
    }

    public void ClearAuthCookies(HttpContext http)
    {
        var expiredOptions = AuthCookiePolicy.Build(_env, DateTimeOffset.UtcNow.AddDays(-1));
        http.Response.Cookies.Delete(AuthCookieNames.AccessToken, expiredOptions);
        http.Response.Cookies.Delete(AuthCookieNames.RefreshToken, expiredOptions);
    }
}
