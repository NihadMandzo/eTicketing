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
