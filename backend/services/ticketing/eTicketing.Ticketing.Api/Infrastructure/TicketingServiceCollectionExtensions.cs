using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Api.Infrastructure.Redis;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using StackExchange.Redis;

namespace eTicketing.Ticketing.Api.Infrastructure;

public static class TicketingServiceCollectionExtensions
{
    public static WebApplicationBuilder AddTicketingInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<TicketingDbContext>(opt => opt
            .UseSqlServer(builder.Configuration.GetConnectionString("TicketingDb"))
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TicketingDbContext>());
        builder.Services.AddScoped<ISectorRepository, SectorRepository>();

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
        builder.Services.AddSingleton<ISectorCapacityLock, RedisSectorCapacityLock>();

        // Ticketing → Catalog: retry + timeout only (no circuit breaker — that's reserved for the
        // future Ticketing → Payment call on the purchase-critical path, per
        // .claude/rules/10-backend.md).
        builder.Services.AddHttpClient<ICatalogClient, HttpCatalogClient>(c =>
                c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!))
            .AddResilienceHandler("catalog-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
                pb.AddTimeout(TimeSpan.FromSeconds(5));
            });

        builder.Services.AddScoped<ISectorService, SectorService>();
        builder.Services.AddValidatorsFromAssembly(typeof(ISectorService).Assembly);
        // Mapster's IRegister configs (SectorMappingConfig, ...) are scanned into
        // TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in eTicketing.Ticketing.Business
        // — see MapsterRegistration.cs — so no explicit call needed.

        // TODO (Sprint 3, US-3.2/3.3): HttpClient + Polly circuit breaker ka eTicketing.Payment.
        // TODO: registrovati ISubscriptionRepository/ISubscriptionService, ITicketRepository/
        // ITicketService i PurchaseService kad buying bude izgrađen (vidi .claude/rules/01-domain.md).

        return builder;
    }
}
