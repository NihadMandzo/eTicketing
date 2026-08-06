using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Payment.Api.Infrastructure;

public static class PaymentServiceCollectionExtensions
{
    public static WebApplicationBuilder AddPaymentInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<PaymentDbContext>(opt => opt
            .UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb"))
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor()));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());

        // TODO (Sprint 3, US-3.2): registrovati IPaymentRepository i IPaymentService kad entiteti budu dodani.

        return builder;
    }
}
