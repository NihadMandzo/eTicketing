using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Business.Payments.Webhooks;
using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Data.Repositories;
using eTicketing.Payment.Data;

namespace eTicketing.Payment.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="PaymentDbContext"/> wired with real repositories (not
/// mocked) — mirrors eTicketing.Ticketing.Business.Tests/TestFixtures/TicketingTestContext.cs.
///
/// The one genuinely external system, the payment provider, is behind <see cref="IPaymentGateway"/>
/// and is mocked here. That is the whole reason the gateway seam exists: no test in this project
/// ever touches the network, so the suite runs offline and without a Stripe account, while the real
/// StripePaymentGateway is exercised by the manual test plan instead.
/// </summary>
public sealed class PaymentTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public PaymentDbContext DbContext { get; }
    public IPaymentRepository PaymentRepository { get; }
    public IStripeEventRepository StripeEventRepository { get; }
    public IStripeCustomerRepository StripeCustomerRepository { get; }
    public IUnitOfWork UnitOfWork { get; }

    public Mock<IPaymentGateway> Gateway { get; } = new(MockBehavior.Strict);
    public Mock<IEventPublisher> EventPublisher { get; } = new();
    public Mock<IStripeSignatureVerifier> SignatureVerifier { get; } = new();

    public StripeOptions StripeOptions { get; } = new()
    {
        SecretKey = "sk_test_fixture",
        PublishableKey = "pk_test_fixture",
        WebhookSecret = "whsec_fixture",
        Currency = "eur",
    };

    public PaymentTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        DbContext = new PaymentDbContext(options);
        DbContext.Database.EnsureCreated();

        PaymentRepository = new PaymentRepository(DbContext);
        StripeEventRepository = new StripeEventRepository(DbContext);
        StripeCustomerRepository = new StripeCustomerRepository(DbContext);
        UnitOfWork = DbContext;

        // Every gateway is one of the two named providers; tests that care override it.
        Gateway.SetupGet(g => g.Name).Returns(PaymentProviderNames.Stripe);
        Gateway.SetupGet(g => g.PublishableKey).Returns("pk_test_fixture");
    }

    public IPaymentService CreatePaymentService() => new PaymentService(
        PaymentRepository,
        StripeCustomerRepository,
        Gateway.Object,
        Options.Create(StripeOptions),
        UnitOfWork,
        NullLogger<PaymentService>.Instance);

    public IStripeWebhookService CreateWebhookService() => new StripeWebhookService(
        SignatureVerifier.Object,
        StripeEventRepository,
        PaymentRepository,
        EventPublisher.Object,
        Options.Create(StripeOptions),
        UnitOfWork,
        NullLogger<StripeWebhookService>.Instance);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
