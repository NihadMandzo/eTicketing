using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace eTicketing.Shared.Auth;

/// <summary>
/// Environment-aware httpOnly cookie policy shared by every endpoint that writes an auth cookie.
///
/// <para><b>SameSite=Strict, everywhere.</b> This used to be <c>None</c> outside development,
/// because production ran the web app and the Gateway as two separate Azure Container Apps on
/// unrelated hostnames — genuinely cross-site, so nothing weaker would have sent the cookie at all.
/// <c>None</c> also meant the session rode along on *any* cross-site request, and the five
/// multipart upload routes are CORS-simple and call <c>DisableAntiforgery()</c>, so a form on
/// another origin could trigger an authenticated upload. Real antiforgery tokens were not an option
/// there: those routes are called from Flutter desktop, which has no way to fetch one.</para>
///
/// <para>The fix was to stop being cross-site. The browser now reaches the API through the web
/// tier's own <c>/api</c> proxy (see frontend/web/src/server.ts), so every request the browser makes
/// is same-origin and <c>Strict</c> is simply satisfied. Local development is covered by the same
/// change (<c>proxy.conf.json</c>), and would have been fine regardless — SameSite compares
/// registrable domains, not ports, so <c>localhost:4200</c> and <c>localhost:5000</c> were always
/// the same site.</para>
///
/// <para><b>The Flutter clients are unaffected either way.</b> <c>SameSite</c> is a browser rule;
/// <c>dio</c> with a persistent cookie jar neither interprets nor enforces it, which is why desktop
/// and mobile keep calling the Gateway directly. See .claude/rules/backend-review-rules.md R44b.</para>
///
/// <para><b>One visible consequence:</b> a cross-site top-level navigation into the web app — the
/// return from Stripe's hosted payment page, or a link in a verification email — does not carry
/// these cookies on that first document request. Nothing breaks: SSR never reads them anyway
/// (app.config.ts's auth initializer is browser-only), and the client-side calls that follow are
/// same-origin and do carry them.</para>
/// </summary>
public static class AuthCookiePolicy
{
    public static CookieOptions Build(IHostEnvironment env, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Path = "/",
        Expires = expires,
        // The one thing that still varies: local development is plain HTTP, and a Secure cookie
        // would never be stored there.
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict
    };
}
