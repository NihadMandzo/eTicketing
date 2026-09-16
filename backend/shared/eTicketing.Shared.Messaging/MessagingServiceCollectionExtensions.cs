using eTicketing.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// The two ways a service can be wired for publishing. Both register the same
/// <see cref="RabbitMqEventPublisher"/> singleton; they differ in what the business layer gets
/// when it asks for <see cref="IEventPublisher"/>.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Transactional outbox. <see cref="IEventPublisher"/> resolves to
    /// <see cref="OutboxEventPublisher{TContext}"/>, so a business method's publish becomes a row in
    /// its own transaction, and <see cref="OutboxDispatcher{TContext}"/> moves those rows to the
    /// broker afterwards.
    ///
    /// <para>The context must map <c>OutboxMessage</c> — apply
    /// <c>OutboxMessageConfiguration</c> in its <c>OnModelCreating</c> and add the migration, or
    /// every publish throws at runtime on a set the model does not know.</para>
    /// </summary>
    public static IServiceCollection AddOutboxMessaging<TContext>(
        this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.AddPlatformRabbitMq(configuration);

        // Scoped, because it writes into the request's own DbContext.
        services.AddScoped<IEventPublisher, OutboxEventPublisher<TContext>>();
        services.AddHostedService<OutboxDispatcher<TContext>>();

        return services;
    }

    /// <summary>
    /// Publish straight to the broker, with no outbox.
    ///
    /// <para>eTicketing.Payment uses this deliberately rather than by omission: StripeWebhookService
    /// publishes *before* it records the delivery, so a failure means Stripe redelivers the whole
    /// webhook and the event is produced again. An outbox would invert that ordering and break the
    /// property it depends on. eTicketing.PdfGeneration likewise has no database of its own to put
    /// an outbox in — it republishes from a message it is still holding, and a failure there simply
    /// nacks and redelivers.</para>
    /// </summary>
    public static IServiceCollection AddDirectMessaging(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPlatformRabbitMq(configuration);
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());

        return services;
    }

    private static IServiceCollection AddPlatformRabbitMq(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqPublisherOptions>(options =>
            options.Host = configuration["RabbitMq:Host"] ?? "localhost");

        // Singleton so the connection it caches is actually shared — a scoped publisher would
        // reopen one per request, which is what the five copies this replaces all did.
        services.AddSingleton<RabbitMqEventPublisher>();
        services.AddSingleton<IRawEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());

        return services;
    }
}
