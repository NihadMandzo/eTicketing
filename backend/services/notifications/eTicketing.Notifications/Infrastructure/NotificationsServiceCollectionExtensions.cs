using eTicketing.Notifications.Messaging;
using eTicketing.Notifications.Options;
using eTicketing.Notifications.Sending;
using eTicketing.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

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

        // Ticket PDFs are attached to the purchase-confirmation email as inline base64, so this
        // consumer has to read them back out of blob storage itself. Not the WebApplicationBuilder
        // AddAzureBlobStorage extension — this is a Worker host, same as PdfGeneration.
        builder.Services.AddOptions<BlobStorageOptions>()
            .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "BlobStorage:ConnectionString mora biti podešen.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        builder.Services.AddSingleton<NotificationDispatcher>();
        builder.Services.AddHostedService<RabbitMqConsumerService>();

        return builder;
    }
}
