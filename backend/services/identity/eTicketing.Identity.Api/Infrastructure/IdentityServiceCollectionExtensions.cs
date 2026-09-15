using System.Text;
using System.Threading.RateLimiting;
using eTicketing.Contracts.Hosting;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Business;
using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Contracts.Security;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Repositories;
using eTicketing.Shared.Storage;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Api.Infrastructure;

/// <summary>
/// All of Identity's DI registration (data, business, messaging) lives here so Program.cs
/// stays a thin composition root. Singleton/scoped/transient lifetimes for repositories,
/// services and infrastructure classes are decided in ONE place, not scattered across
/// Program.cs.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>The caller's address, or a single shared bucket when there isn't one. "unknown" is
    /// deliberately one partition rather than a per-request key: an address-less caller should be
    /// throttled together with every other address-less caller, not handed its own allowance.</summary>
    private static string PartitionKeyFor(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static WebApplicationBuilder AddIdentityInfrastructure(this WebApplicationBuilder builder)
    {
        // --- Data sloj ---
        // Read (and fail fast on) the connection string here, at composition time, rather than
        // letting a missing value surface later as a NullReferenceException/SqlException on the
        // first request that resolves IdentityDbContext.
        var identityConnectionString = builder.Configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(identityConnectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' nije konfigurisan.");
        }

        builder.Services.AddDbContext<IdentityDbContext>(opt => opt
            .UseSqlServer(identityConnectionString)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        // --- Business sloj ---
        // ValidateOnStart forces this to run during boot (not lazily on the first token issue),
        // so a deployment shipping the placeholder/short SigningKey fails immediately instead of
        // throwing a SecurityTokenException on the first login.
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Jwt:SigningKey mora biti podešen i imati najmanje 32 bajta (HS256 minimum).")
            .ValidateOnStart();
        builder.AddAzureBlobStorage();
        builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IOrganizationService, OrganizationService>();
        builder.Services.AddScoped<IAdminService, AdminService>();
        builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
        builder.Services.AddValidatorsFromAssembly(typeof(IAuthService).Assembly);
        // Registers the shared BaseSearchObjectValidator (for BaseSearchObject) — kept
        // registered for any future route that binds BaseSearchObject directly without a
        // derived query type (no current route does; GetUsers used to before it gained a Role
        // filter and moved to OrganizationUserQuery).
        builder.Services.AddValidatorsFromAssembly(typeof(BaseSearchObjectValidator).Assembly);
        // Mapster's IRegister configs (UserMappingConfig, OrganizationMappingConfig, ...) are
        // scanned into TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in
        // eTicketing.Identity.Business — see MapsterRegistration.cs — so no explicit call is
        // needed here.

        // --- Messaging ---
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        // --- Rate limiting ---
        // Every partition key below is the caller's IP, which is only actually the caller's IP
        // because AddPlatformForwardedHeaders + UseForwardedHeaders rewrite RemoteIpAddress from
        // X-Forwarded-For. Without that pair every request arrives from the Gateway's container
        // address and all of these collapse into a single platform-wide bucket — which is what
        // they did before. See ForwardedHeadersExtensions for the trust boundary that involves.
        builder.Services.AddPlatformForwardedHeaders(builder.Configuration);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Applied to /auth/forgot-password and /auth/resend-verification-email (see
            // AuthEndpoints) — both trigger an outbound email with no other cooldown, so without
            // this a script can flood a victim's inbox or burn through the platform's email-send
            // quota. One policy for both routes rather than a second, user-id-keyed one just for
            // the authenticated resend-verification-email route.
            options.AddPolicy(AuthRateLimitPolicies.EmailSending, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: PartitionKeyFor(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 3,
                    QueueLimit = 0
                }));

            // Login and register had no limit at all, and there is no account lockout anywhere in
            // this service — so an unthrottled /auth/login was an open credential-stuffing target
            // and /auth/register an open account-flooding one.
            //
            // Partitioned by IP alone, deliberately, even though IP+email would be the better key:
            // both routes carry the email in the JSON body, and a partition factory cannot read it
            // without turning on request buffering for every auth call. A key that reached for a
            // query parameter neither client sends would quietly be IP-only anyway, while reading
            // as though it were not — worse than choosing IP honestly.
            //
            // The cost of IP-only is that a shared NAT is one bucket, so the limit is sized for an
            // office rather than a person: 20/minute is well above several colleagues signing in at
            // once and well below a script's rate. Narrowing it is what real per-account lockout is
            // for, and this service has none yet.
            options.AddPolicy(AuthRateLimitPolicies.AuthAttempts, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: PartitionKeyFor(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 20,
                    QueueLimit = 0
                }));

            // A rejected request used to answer 429 with an *empty body*, which no client can parse
            // — all three frontends bind their error text from { code, message }, so a throttled
            // login surfaced as a blank failure. Matching the shape ResultExtensions.ToHttpResult
            // emits means they render it like any other refusal, with no client-side special case.
            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
                    ? (int)window.TotalSeconds
                    : (int)TimeSpan.FromMinutes(1).TotalSeconds;

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        Code = "rate_limit.exceeded",
                        Message = $"Previše pokušaja. Pokušajte ponovo za {retryAfter} sekundi."
                    },
                    ct);
            };
        });

        return builder;
    }
}
