using eTicketing.Notifications.Infrastructure.Redis;
using eTicketing.Notifications.Messaging;
using eTicketing.Notifications.Options;
using eTicketing.Notifications.Sending;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using StackExchange.Redis;

namespace eTicketing.Notifications.Infrastructure;

/// <summary>All of Notifications' DI registration lives here so Program.cs stays a thin
/// composition root, same convention as every other service's
/// Infrastructure/&lt;Service&gt;ServiceCollectionExtensions.cs.</summary>
public static class NotificationsServiceCollectionExtensions
{
    public static HostApplicationBuilder AddNotificationsInfrastructure(this HostApplicationBuilder builder)
    {
        builder.Services.AddOptions<RabbitMqOptions>()
            .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        builder.Services.AddOptions<FrontendOptions>()
            .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName));

        // ValidateOnStart: a deployment shipping without a real Brevo API key fails immediately
        // at boot instead of silently swallowing every send behind RabbitMqConsumerService's
        // retry-forever logic (which would otherwise make a missing key indistinguishable from a
        // real Brevo outage).
        builder.Services.AddOptions<BrevoOptions>()
            .Bind(builder.Configuration.GetSection(BrevoOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Brevo:ApiKey mora biti podešen.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SenderEmail), "Brevo:SenderEmail mora biti podešen.")
            .ValidateOnStart();

        // Deliberately built from options here, not passed as IOptions<BrevoOptions> into
        // BrevoEmailSender — this snapshot never carries ApiKey, so that class has no code path
        // to the key value at all (see BrevoOptionsSnapshot's doc comment).
        builder.Services.AddSingleton(sp =>
        {
            var brevo = sp.GetRequiredService<IOptions<BrevoOptions>>().Value;
            return new BrevoOptionsSnapshot(brevo.SenderEmail, brevo.SenderName);
        });

        builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>((sp, client) =>
            {
                var brevo = sp.GetRequiredService<IOptions<BrevoOptions>>().Value;
                client.BaseAddress = new Uri("https://api.brevo.com/v3/");
                // Set once, here, from configuration — never read back out of the HttpClient or
                // passed into BrevoEmailSender itself.
                client.DefaultRequestHeaders.Add("api-key", brevo.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddResilienceHandler("brevo-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(1)
                });
                pb.AddTimeout(TimeSpan.FromSeconds(10));
            });

        // Required, not optional: without it every redelivered event re-sends its email. Missing
        // configuration fails the boot loudly, same as a missing Brevo key above. A configured but
        // unreachable Redis does not — AbortOnConnectFail=false lets the service start and keep
        // reconnecting, and DeduplicatingDeliveryHandler sends without dedupe in the meantime.
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new InvalidOperationException("ConnectionStrings:Redis mora biti podešen.");

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        builder.Services.AddSingleton<IProcessedMessageStore, RedisProcessedMessageStore>();

        builder.Services.AddSingleton<NotificationDispatcher>();
        builder.Services.AddSingleton<DeduplicatingDeliveryHandler>();
        builder.Services.AddHostedService<RabbitMqConsumerService>();

        return builder;
    }
}
