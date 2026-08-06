using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Api.Infrastructure;

public static class TicketingServiceCollectionExtensions
{
    public static WebApplicationBuilder AddTicketingInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<TicketingDbContext>(opt => opt
            .UseSqlServer(builder.Configuration.GetConnectionString("TicketingDb"))
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TicketingDbContext>());

        // TODO (Sprint 2, US-2.4/2.5): registrovati ISectorRepository, ISectorCapacityLock (Redis) i
        // ISectorService iz .Business projekta ovdje, kad entiteti budu dodani.
        // TODO (Sprint 3, US-3.2/3.3): HttpClient + Polly circuit breaker ka eTicketing.Payment,
        // HttpClient + Polly retry/timeout ka eTicketing.Catalog (provjera vlasništva eventa).

        return builder;
    }
}
