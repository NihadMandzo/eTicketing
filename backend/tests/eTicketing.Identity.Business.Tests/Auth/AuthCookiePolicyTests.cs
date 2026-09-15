using eTicketing.Shared.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;

namespace eTicketing.Identity.Business.Tests.Auth;

/// <summary>
/// The attributes on the session cookies, pinned. Every one of these is a security property whose
/// failure is silent: a cookie missing HttpOnly is still sent and still works, and one that slid
/// back to SameSite=None still logs users in — it just also rides along on cross-site requests to
/// the five multipart routes that deliberately have no antiforgery token.
/// </summary>
public class AuthCookiePolicyTests
{
    private static IHostEnvironment Environment(string environmentName)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return env.Object;
    }

    private static CookieOptions Build(string environmentName) =>
        AuthCookiePolicy.Build(Environment(environmentName), DateTimeOffset.UtcNow.AddMinutes(15));

    // Literals rather than the Environments.* fields: those are static readonly, not const, so
    // they cannot appear in an attribute argument.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Build_IsAlwaysHttpOnlyAndStrict(string environmentName)
    {
        var options = Build(environmentName);

        // HttpOnly is what keeps the tokens out of JS reach — the entire reason this platform
        // moved off bearer-in-localStorage.
        options.HttpOnly.Should().BeTrue();

        // Strict, not None. It became possible when the web tier started proxying /api itself
        // (frontend/web/src/server.ts), making the browser same-origin with the API. SameSite
        // compares registrable domains and ignores ports, so local development satisfies it too.
        options.SameSite.Should().Be(SameSiteMode.Strict);

        options.Path.Should().Be("/");
    }

    [Fact]
    public void Build_OutsideDevelopment_RequiresSecure()
    {
        Build(Environments.Production).Secure.Should().BeTrue();
    }

    [Fact]
    public void Build_InDevelopment_DoesNotRequireSecure()
    {
        // The one environment-dependent attribute: local development is plain HTTP and a Secure
        // cookie would never be stored at all, which would make every dev login silently fail.
        Build(Environments.Development).Secure.Should().BeFalse();
    }

    [Fact]
    public void Build_CarriesTheExpiryItWasGiven()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(7);

        AuthCookiePolicy.Build(Environment(Environments.Production), expires)
            .Expires.Should().Be(expires);
    }
}
