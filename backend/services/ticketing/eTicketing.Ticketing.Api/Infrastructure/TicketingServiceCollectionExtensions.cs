using System.Net.Http.Headers;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Api.Infrastructure.Auth;
using eTicketing.Ticketing.Api.Infrastructure.Messaging;
using eTicketing.Ticketing.Api.Infrastructure.Redis;
using eTicketing.Ticketing.Api.Infrastructure.TicketPrint;
using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Anomalies;
using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Analytics.Insights;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.Analytics.Segmentation;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Subscriptions;
using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data;
using eTicketing.Ticketing.Data.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using QuestPDF.Infrastructure;
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
        builder.Services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
        builder.Services.AddScoped<ITicketRepository, TicketRepository>();
        builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        builder.Services.AddScoped<ITicketPrintBatchRepository, TicketPrintBatchRepository>();
        builder.Services.AddScoped<IGateDeviceRepository, GateDeviceRepository>();

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
        builder.Services.AddSingleton<ISectorCapacityLock, RedisSectorCapacityLock>();
        builder.Services.AddSingleton<ITicketValidationLock, RedisTicketValidationLock>();

        // GET /tickets/{id}/pdf renders the buyer's sheet on demand rather than serving a stored
        // file, so this service needs the QuestPDF licence and the embedded design fonts — same
        // registration eTicketing.PdfGeneration does, from the same shared project.
        QuestPDF.Settings.License = LicenseType.Community;
        TicketTheme.EnsureFontsRegistered();

        builder.Services.AddOptions<TicketSupportOptions>()
            .Bind(builder.Configuration.GetSection(TicketSupportOptions.SectionName));

        // Singleton: the codec is a stateless HMAC over one immutable key. Fails fast at startup if
        // the key is missing rather than minting unverifiable QR codes at purchase time.
        builder.Services.AddSingleton(_ => new TicketQrCodec(builder.Configuration["Qr:SigningKey"]!));
        builder.Services.AddScoped<TicketResponseFactory>();

        // TimeProvider.System — injected rather than DateTime.UtcNow so "is this ticket valid
        // today" is testable without waiting for midnight.
        builder.Services.AddSingleton(TimeProvider.System);

        // PlatformClock answers "what day is it here" in the deployment's own time zone. Every date
        // the domain compares against (Ticket.ValidDate, ValidFrom/ValidTo, Product.Date) is a local
        // wall-clock date, so a UTC-derived "today" turns valid tickets away at the gate for the
        // 1-2 hours between local and UTC midnight. Singleton, and it resolves the zone eagerly, so
        // a bad/missing time zone fails at startup rather than silently at the door.
        builder.Services.AddOptions<PlatformTimeOptions>()
            .Bind(builder.Configuration.GetSection(PlatformTimeOptions.SectionName));
        builder.Services.AddSingleton<PlatformClock>();

        // Ticketing → Catalog: retry + timeout only — no circuit breaker here. The circuit breaker
        // is reserved for the Ticketing → Payment call below, since that's the one call on the
        // synchronous purchase-critical path (per .claude/rules/10-backend.md).
        builder.Services.AddHttpClient<ICatalogClient, HttpCatalogClient>(c =>
                c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!))
            .AddResilienceHandler("catalog-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
                pb.AddTimeout(TimeSpan.FromSeconds(5));
            });

        // Ticketing → Identity: same shape as the Catalog client above. Only the Izvještaji
        // reports need it — every other org-scoped decision in this service reads organizationId
        // straight off the caller's token and never has to ask Identity anything.
        builder.Services.AddHttpClient<IIdentityClient, HttpIdentityClient>(c =>
                c.BaseAddress = new Uri(builder.Configuration["Services:Identity"]!))
            .AddResilienceHandler("identity-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
                pb.AddTimeout(TimeSpan.FromSeconds(5));
            });

        builder.Services.AddScoped<ISectorService, SectorService>();
        builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
        builder.Services.AddScoped<ITicketService, TicketService>();
        builder.Services.AddScoped<ITicketValidationService, TicketValidationService>();
        builder.Services.AddScoped<ITicketPdfService, TicketPdfService>();
        builder.Services.AddScoped<IPurchaseService, PurchaseService>();
        builder.Services.AddScoped<ITicketPdfCompletionService, TicketPdfCompletionService>();
        builder.Services.AddScoped<IProductChangeNotifier, ProductChangeNotifier>();
        builder.Services.AddScoped<IProductDeletionNotifier, ProductDeletionNotifier>();
        builder.Services.AddScoped<ITicketPrintService, TicketPrintService>();
        builder.Services.AddScoped<ITicketPrintRenderer, TicketPrintRenderer>();
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
        builder.Services.AddScoped<IGateDeviceService, GateDeviceService>();

        // Stateless SHA-256 + CSPRNG wrapper, same reasoning as TicketQrCodec's singleton above.
        builder.Services.AddSingleton<GateDeviceKeyGenerator>();

        // The queue is a singleton the worker reads and request threads write; ITicketPrintQueue is
        // what Business depends on, and the worker needs the concrete type for its ChannelReader.
        builder.Services.AddSingleton<TicketPrintQueue>();
        builder.Services.AddSingleton<ITicketPrintQueue>(sp => sp.GetRequiredService<TicketPrintQueue>());
        builder.Services.AddHostedService<TicketPrintRenderWorker>();
        builder.Services.AddHostedService<TicketingRabbitMqConsumerService>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<ISubscriptionRenewalService, SubscriptionRenewalService>();
        builder.Services.AddValidatorsFromAssembly(typeof(ISectorService).Assembly);
        // Mapster's IRegister configs (SectorMappingConfig, ...) are scanned into
        // TypeAdapterConfig.GlobalSettings by a [ModuleInitializer] in eTicketing.Ticketing.Business
        // — see MapsterRegistration.cs — so no explicit call needed.

        // Ticketing → Payment: the project's flagship circuit-breaker example, since this is the
        // one call on the synchronous purchase-critical path (see docs/payment-setup-guide.md §4).
        // Retry outermost, CircuitBreaker, Timeout innermost — the documented standard order for
        // AddResilienceHandler.
        builder.Services.AddHttpClient<IPaymentClient, HttpPaymentClient>(c =>
                c.BaseAddress = new Uri(builder.Configuration["Services:Payment"]!))
            .AddResilienceHandler("payment-pipeline", pb =>
            {
                pb.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromMilliseconds(200),
                });
                pb.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    FailureRatio = 1.0,
                    BreakDuration = TimeSpan.FromSeconds(15),
                });
                // 10s, not the 5s the other pipelines use: this call is no longer a local mock. With
                // Payment:Provider=Stripe it makes a real round trip to the payment provider, and a
                // 5s budget would trip the circuit on ordinary provider latency rather than on a
                // genuine outage -- turning a slow payment into a false "plaćanje nije dostupno".
                pb.AddTimeout(TimeSpan.FromSeconds(10));
            });

        builder.AddAnalyticsInfrastructure();

        return builder;
    }

    /// <summary>
    /// The AI Uvidi tab (GET /reports/insights).
    ///
    /// Note what is <i>not</i> here: no hosted service, no blob container, no snapshot table. Unlike
    /// Catalog's recommender, whose matrix factorization is expensive enough to need a nightly job
    /// and a persisted model, SSA and K-Means are fitted per request on the very range the caller
    /// selected — a stored model would have been fitted on somebody else's window. The three
    /// components are singletons because they hold nothing but a logger.
    /// </summary>
    private static WebApplicationBuilder AddAnalyticsInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<InsightsOptions>()
            .Bind(builder.Configuration.GetSection(InsightsOptions.SectionName));

        // AnalyticsService caches a computed response for a few minutes: the tab costs three report
        // queries plus two cross-service lookups, and none of that changes while the user is
        // flipping between horizons on the same range.
        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<ISalesForecaster, SsaSalesForecaster>();
        builder.Services.AddSingleton<IAnomalyDetector, SsaAnomalyDetector>();
        builder.Services.AddSingleton<IAudienceSegmenter, KMeansAudienceSegmenter>();
        builder.Services.AddSingleton<IInsightGenerator, InsightGenerator>();
        builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

        var narrative = builder.Configuration
            .GetSection(InsightsOptions.SectionName)
            .Get<InsightsOptions>()?.Narrative ?? new NarrativeOptions();

        if (!narrative.IsEnabled)
        {
            // The default, and what every test and offline demo runs with: the tab returns its
            // deterministic insights and no summary. Nothing else about the feature changes.
            builder.Services.AddSingleton<INarrativeWriter, NullNarrativeWriter>();
            return builder;
        }

        // Timeout only — no retry, deliberately, unlike every other client in this service.
        //
        // The failure this call actually has is slowness, not flakiness: a local 3B model on CPU
        // takes over a minute for the 8-10 sentence summary. Retrying that asks the same question of the same
        // machine and waits the same time again, so a single retry turned a 20s ceiling into a 45s
        // one on a report the user is watching load. Measured, not theorised. A genuinely transient
        // network blip costs the summary and nothing else, which is the trade this whole seam exists
        // to make.
        //
        // The timeout comes from configuration because a local 3B model and a hosted 70B one are an
        // order of magnitude apart.
        builder.Services.AddHttpClient<INarrativeWriter, OpenAiCompatibleNarrativeWriter>(c =>
            {
                // Trailing slash matters: the relative "chat/completions" would otherwise replace
                // the "/v1" segment rather than extend it.
                c.BaseAddress = new Uri(narrative.BaseUrl.TrimEnd('/') + "/");

                // Ollama authenticates nothing; Groq and OpenRouter need a bearer token. Sent only
                // when one is actually configured, so a stray empty header never reaches Ollama.
                if (!string.IsNullOrWhiteSpace(narrative.ApiKey))
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", narrative.ApiKey);
            })
            .AddResilienceHandler("narrative-pipeline", pb =>
                pb.AddTimeout(TimeSpan.FromSeconds(Math.Clamp(narrative.TimeoutSeconds, 5, 60))));

        return builder;
    }
}
