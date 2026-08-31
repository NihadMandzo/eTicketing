using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Api.Infrastructure.Messaging;
using eTicketing.Ticketing.Api.Infrastructure.Redis;
using eTicketing.Ticketing.Api.Infrastructure.TicketPrint;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.Sectors;
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
        builder.Services.AddScoped<ITicketPrintService, TicketPrintService>();
        builder.Services.AddScoped<ITicketPrintRenderer, TicketPrintRenderer>();
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddScoped<IReportPdfService, ReportPdfService>();

        // The queue is a singleton the worker reads and request threads write; ITicketPrintQueue is
        // what Business depends on, and the worker needs the concrete type for its ChannelReader.
        builder.Services.AddSingleton<TicketPrintQueue>();
        builder.Services.AddSingleton<ITicketPrintQueue>(sp => sp.GetRequiredService<TicketPrintQueue>());
        builder.Services.AddHostedService<TicketPrintRenderWorker>();
        builder.Services.AddHostedService<TicketingRabbitMqConsumerService>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
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
                pb.AddTimeout(TimeSpan.FromSeconds(5));
            });

        return builder;
    }
}
