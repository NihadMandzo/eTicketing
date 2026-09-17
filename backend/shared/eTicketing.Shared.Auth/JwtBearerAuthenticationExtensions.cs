using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace eTicketing.Shared.Auth;

/// <summary>
/// The single, deduplicated JWT-bearer setup every web-facing service (Gateway, Identity.Api,
/// Catalog.Api, Ticketing.Api) shares — previously this exact block was copy-pasted 4x.
/// Reads the access token from the httpOnly <see cref="AuthCookieNames.AccessToken"/> cookie
/// instead of the Authorization header, since the platform is cookie-based, not bearer-in-body.
/// </summary>
public static class JwtBearerAuthenticationExtensions
{
    public static IServiceCollection AddSharedJwtBearerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Jwt").Get<JwtValidationOptions>()
            ?? throw new InvalidOperationException("Jwt konfiguracija nedostaje.");

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey nije konfigurisan.");
        }

        // HS256 needs a key at least as long as its output (256 bits = 32 bytes) to meet the
        // algorithm's security requirement; a shorter key weakens the token signature well below
        // what HS256 is supposed to provide.
        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey mora imati najmanje 32 bajta (HS256 minimum).");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearerOptions =>
            {
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AuthCookieNames.AccessToken, out var token))
                        {
                            context.Token = token;
                            return Task.CompletedTask;
                        }

                        // No cookie means unauthenticated — full stop. Simply returning here (or
                        // assigning an empty token) would let JwtBearerHandler fall through to its
                        // default Authorization: Bearer header read, since it only reaches for the
                        // header when context.Token is null-or-empty. That fallback is exactly the
                        // bearer-in-header path this platform replaced with httpOnly cookies, so it
                        // has to be closed explicitly rather than left to the handler's default.
                        context.NoResult();
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
