using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Business.Payments.Webhooks;
using eTicketing.Payment.Data;
using eTicketing.Payment.Data.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

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
        builder.Services.AddScoped<IStripeEventRepository, StripeEventRepository>();
        builder.Services.AddScoped<IStripeCustomerRepository, StripeCustomerRepository>();

        builder.Services.AddOptions<PaymentOptions>()
            .Bind(builder.Configuration.GetSection(PaymentOptions.SectionName));
        builder.Services.AddOptions<StripeOptions>()
            .Bind(builder.Configuration.GetSection($"{PaymentOptions.SectionName}:Stripe"));

        // Built from configuration rather than the StripeConfiguration.ApiKey static, so nothing
        // process-global has to be mutated and a test can substitute a client freely.
        builder.Services.AddSingleton<IStripeClient>(sp =>
            new StripeClient(sp.GetRequiredService<IOptions<StripeOptions>>().Value.SecretKey));

        // Both gateways are registered unconditionally but only one is ever RESOLVED, so an empty
        // SecretKey never breaks a Mock-mode boot -- which is what makes PAYMENT_PROVIDER=Mock a
        // genuine rollback rather than a configuration that still needs Stripe credentials present.
        builder.Services.AddScoped<MockPaymentGateway>();
        builder.Services.AddScoped<StripePaymentGateway>();
        builder.Services.AddScoped<IPaymentGateway>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<PaymentOptions>>().Value.Provider;

            return string.Equals(provider, PaymentProviderNames.Stripe, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<StripePaymentGateway>()
                // Anything else, including an empty or misspelled value, degrades to the offline
                // gateway rather than failing to start.
                : sp.GetRequiredService<MockPaymentGateway>();
        });

        builder.Services.AddSingleton<IStripeSignatureVerifier, StripeSignatureVerifier>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        builder.Services.AddScoped<IPaymentService, PaymentService>();
        builder.Services.AddValidatorsFromAssembly(typeof(IPaymentService).Assembly);

        return builder;
    }
}
