using QuestPDF.Infrastructure;
using eTicketing.PdfGeneration.Documents;
using eTicketing.PdfGeneration.Messaging;
using eTicketing.PdfGeneration.Options;
using eTicketing.Shared.Messaging;
using eTicketing.Shared.TicketPdf;

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

        // Manrope / IBM Plex Mono are embedded in this assembly and must be known to QuestPDF
        // before the first render. Registering at startup surfaces a packaging mistake as a
        // failure to boot rather than as a silently font-substituted ticket.
        TicketTheme.EnsureFontsRegistered();

        builder.Services.AddOptions<RabbitMqOptions>()
            .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        builder.Services.AddOptions<TicketSupportOptions>()
            .Bind(builder.Configuration.GetSection(TicketSupportOptions.SectionName));

        // No HTTP client of any kind here any more: this service talks to the broker and to blob
        // storage, and nothing else. The product details the sheet needs arrive on TicketPurchased.
        builder.Services.AddSingleton<ITicketPdfGenerator, TicketPdfGenerator>();
        // Direct to the broker, no outbox — this service has no database to put one in. It
        // republishes from a message it is still holding, so a failure nacks and redelivers.
        builder.Services.AddDirectMessaging(builder.Configuration);
        builder.Services.AddSingleton<TicketPurchasedDispatcher>();
        builder.Services.AddHostedService<RabbitMqConsumerService>();

        return builder;
    }
}
