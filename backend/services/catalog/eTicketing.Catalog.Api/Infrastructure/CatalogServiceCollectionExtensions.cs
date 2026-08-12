using eTicketing.Catalog.Business;
using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Events;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using FluentValidation;
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
        builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
        builder.Services.AddScoped<IEventRepository, EventRepository>();

        builder.Services.AddOptions<CatalogOptions>()
            .Bind(builder.Configuration.GetSection(CatalogOptions.SectionName));

        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<IEventService, EventService>();
        builder.Services.AddValidatorsFromAssembly(typeof(ICategoryService).Assembly);
        // Mapster's IRegister configs (CategoryMappingConfig, EventMappingConfig) are scanned
        // into TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in
        // eTicketing.Catalog.Business — see MapsterRegistration.cs — so no explicit call needed.

        return builder;
    }
}
