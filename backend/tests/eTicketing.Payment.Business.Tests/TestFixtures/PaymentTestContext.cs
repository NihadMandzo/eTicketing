using eTicketing.Contracts.Persistence;
using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Data;
using eTicketing.Payment.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Payment.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="PaymentDbContext"/> wired with the real
/// <see cref="PaymentRepository"/> (not mocked) — mirrors
/// eTicketing.Ticketing.Business.Tests/TestFixtures/TicketingTestContext.cs. PaymentService has no
/// genuine external systems to mock — it's the deterministic mock itself (see
/// docs/payment-setup-guide.md).
/// </summary>
public sealed class PaymentTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public PaymentDbContext DbContext { get; }
    public IPaymentRepository PaymentRepository { get; }
    public IUnitOfWork UnitOfWork { get; }

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
        UnitOfWork = DbContext;
    }

    public IPaymentService CreatePaymentService() => new PaymentService(PaymentRepository, UnitOfWork);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
