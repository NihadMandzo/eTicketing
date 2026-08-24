using eTicketing.PdfGeneration.Documents;
using eTicketing.PdfGeneration.External;
using eTicketing.PdfGeneration.Messaging;
using eTicketing.PdfGeneration.Options;
using eTicketing.Shared.Storage;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using QuestPDF.Infrastructure;

namespace eTicketing.PdfGeneration.Infrastructure;

/// <summary>All of PdfGeneration's DI registration lives here so Program.cs stays a thin
/// composition root, same convention as every other service's
/// Infrastructure/&lt;Service&gt;ServiceCollectionExtensions.cs.</summary>
public static class PdfGenerationServiceCollectionExtensions
{
    public static HostApplicationBuilder AddPdfGenerationInfrastructure(this HostApplicationBuilder builder)
    {
        // QuestPDF refuses to render a single page until a license type is declared. Community is
        // the correct one for a thesis project (see SPRINT_4 US-4.2) and must be set before the
        // first GeneratePdf call, so it goes here rather than lazily in the generator.
        QuestPDF.Settings.License = LicenseType.Community;

        builder.Services.AddOptions<RabbitMqOptions>()
            .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        // Not the WebApplicationBuilder-based AddAzureBlobStorage extension — this is a Worker
        // host, so the two registrations that helper makes are done by hand here.
        builder.Services.AddOptions<BlobStorageOptions>()
            .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "BlobStorage:ConnectionString mora biti podešen.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        // Retry + timeout, no circuit breaker: this is an async consumer with its own backoff
        // ladder behind it, not a synchronous critical path that needs to fail fast.
        builder.Services.AddHttpClient<ICatalogClient, HttpCatalogClient>(c =>
                c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!))
            .AddResilienceHandler("catalog-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
                pb.AddTimeout(TimeSpan.FromSeconds(5));
            });

        builder.Services.AddSingleton<ITicketPdfGenerator, TicketPdfGenerator>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddSingleton<TicketPurchasedDispatcher>();
        builder.Services.AddHostedService<RabbitMqConsumerService>();

        return builder;
    }
}
