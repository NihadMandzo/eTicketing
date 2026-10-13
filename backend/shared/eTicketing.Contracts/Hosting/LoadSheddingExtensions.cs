using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Hosting;

/// <summary>Registration and pipeline placement for <see cref="LoadSheddingMiddleware"/>.</summary>
public static class LoadSheddingExtensions
{
    public static IServiceCollection AddPlatformLoadShedding(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LoadSheddingOptions>(configuration.GetSection(LoadSheddingOptions.SectionName));
        return services;
    }

    /// <summary>Call right after <c>UseExceptionHandler</c>, before authentication: a request that
    /// is going to be refused should not pay for JWT validation first.</summary>
    public static IApplicationBuilder UsePlatformLoadShedding(this IApplicationBuilder app) =>
        app.UseMiddleware<LoadSheddingMiddleware>();
}
