using eTicketing.Contracts.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Cross-cutting API boilerplate shared by every web-facing service (Identity, Catalog,
/// Ticketing, Payment): global exception handling, health checks, OpenAPI. Keeps each
/// service's own Program.cs minimal.
/// </summary>
public static class ApiHostingExtensions
{
    public static WebApplicationBuilder AddPlatformApiEssentials<TContext>(this WebApplicationBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks().AddDbContextCheck<TContext>("database");
        builder.Services.AddOpenApi();

        return builder;
    }
}
