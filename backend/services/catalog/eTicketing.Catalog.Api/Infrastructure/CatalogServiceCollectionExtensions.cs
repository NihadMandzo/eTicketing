using eTicketing.Catalog.Data;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Api.Infrastructure;

public static class CatalogServiceCollectionExtensions
{
    public static WebApplicationBuilder AddCatalogInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<CatalogDbContext>(opt => opt
            .UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb"))
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>());

        // TODO (Sprint 2, US-2.1/2.2/2.3): registrovati ICategoryRepository/IEventRepository i
        // ICategoryService/IEventService iz .Business projekta ovdje, kad entiteti budu dodani.

        return builder;
    }
}
