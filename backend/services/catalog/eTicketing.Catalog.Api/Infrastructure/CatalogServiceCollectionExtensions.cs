using eTicketing.Catalog.Business;
using eTicketing.Catalog.Api.Infrastructure.Messaging;
using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Products.Mapping;
using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Data;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using eTicketing.Shared.Messaging;
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
        builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
        builder.Services.AddScoped<IUserInteractionRepository, UserInteractionRepository>();
        builder.Services.AddScoped<IRecommendationModelSnapshotRepository, RecommendationModelSnapshotRepository>();

        builder.AddAzureBlobStorage();

        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<ProductResponseFactory>();
        // Transactional outbox: a publish from the business layer becomes a row in the same
        // SaveChangesAsync as the data that caused it, and a hosted dispatcher moves those rows to
        // the broker. See eTicketing.Shared.Messaging.OutboxEventPublisher for why the ordering of
        // publish-then-save now matters.
        builder.Services.AddOutboxMessaging<CatalogDbContext>(builder.Configuration);
        // Its inbound counterpart: CatalogRabbitMqConsumerService records each processed purchase in
        // the same transaction as the interaction it bumps, so a redelivery is not counted twice.
        builder.Services.AddTransactionalInbox<CatalogDbContext>();

        // Recommendations. The model is a singleton because it is expensive to build and shared by
        // every request; everything around it is scoped like the rest of the service.
        builder.Services.Configure<RecommendationOptions>(builder.Configuration.GetSection("Recommendations"));
        builder.Services.AddSingleton<IRecommendationModel, MatrixFactorizationModel>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IRecommendationService, RecommendationService>();
        builder.Services.AddScoped<PurchaseInteractionRecorder>();
        builder.Services.AddHostedService<RecommendationTrainingHostedService>();
        builder.Services.AddHostedService<CatalogRabbitMqConsumerService>();
        builder.Services.AddValidatorsFromAssembly(typeof(ICategoryService).Assembly);
        // Mapster's IRegister configs (CategoryMappingConfig, ProductMappingConfig) are scanned
        // into TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in
        // eTicketing.Catalog.Business — see MapsterRegistration.cs — so no explicit call needed.

        return builder;
    }
}
