using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Data;
using eTicketing.Payment.Data.Repositories;
using FluentValidation;
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
        builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
        builder.Services.AddScoped<IPaymentService, PaymentService>();
        builder.Services.AddValidatorsFromAssembly(typeof(IPaymentService).Assembly);

        return builder;
    }
}
