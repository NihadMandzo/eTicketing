using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace eTicketing.Shared.Auth;

/// <summary>
/// Environment-aware httpOnly cookie policy shared by every endpoint that writes an auth
/// cookie. In dev, the Angular dev server (:4200) and the Gateway (:5262) are cross-port but
/// same-site ("localhost"), so SameSite=Lax is sent on XHR/fetch without requiring an HTTPS
/// dev certificate. In non-dev, web/API can live on different registrable domains, so
/// SameSite=None + Secure is required.
/// </summary>
public static class AuthCookiePolicy
{
    public static CookieOptions Build(IHostEnvironment env, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Path = "/",
        Expires = expires,
        Secure = !env.IsDevelopment(),
        SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None
    };
}
