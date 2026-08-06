using System.Text;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Repositories;
using FluentValidation;
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
        builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IOrganizationService, OrganizationService>();
        builder.Services.AddScoped<IAdminService, AdminService>();
        builder.Services.AddValidatorsFromAssembly(typeof(IAuthService).Assembly);
        // Registers the shared BaseSearchObjectValidator (for BaseSearchObject) used directly
        // by routes like GetUsers that bind BaseSearchObject without a derived query type.
        builder.Services.AddValidatorsFromAssembly(typeof(BaseSearchObjectValidator).Assembly);

        // --- Messaging ---
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return builder;
    }
}
