using eTicketing.Catalog.Business;
using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Storage;
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
        builder.Services.AddScoped<IProductRepository, ProductRepository>();

        builder.AddAzureBlobStorage();

        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddValidatorsFromAssembly(typeof(ICategoryService).Assembly);
        // Mapster's IRegister configs (CategoryMappingConfig, ProductMappingConfig) are scanned
        // into TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in
        // eTicketing.Catalog.Business — see MapsterRegistration.cs — so no explicit call needed.

        return builder;
    }
}
