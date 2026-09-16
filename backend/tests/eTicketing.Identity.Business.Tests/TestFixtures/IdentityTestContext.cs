using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data.Repositories;
using eTicketing.Identity.Data;
using eTicketing.Shared.Messaging;

namespace eTicketing.Identity.Business.Tests.TestFixtures;

/// <summary>
/// A real EF Core Sqlite in-memory <see cref="IdentityDbContext"/> wired with the real
/// repositories (not mocked) so unique-index/FK behavior is exercised for real, matching
/// the plan's "run against Sqlite in-memory, not mocked repositories" approach. The only
/// mocked dependency is <see cref="IEventPublisher"/> (a genuine external system — RabbitMQ).
/// </summary>
public sealed class IdentityTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public IdentityDbContext DbContext { get; }
    public IUserRepository UserRepository { get; }
    public IOrganizationRepository OrganizationRepository { get; }
    public IRefreshTokenRepository RefreshTokenRepository { get; }
    public IPasswordResetTokenRepository PasswordResetTokenRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public IJwtTokenGenerator TokenGenerator { get; }
    public JwtOptions JwtOptions { get; }
    public FakeBlobStorageService BlobStorage { get; } = new();
    public Mock<IEventPublisher> EventPublisherMock { get; } = new();

    public IdentityTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        DbContext = new IdentityDbContext(options);
        DbContext.Database.EnsureCreated();

        UserRepository = new UserRepository(DbContext);
        OrganizationRepository = new OrganizationRepository(DbContext);
        RefreshTokenRepository = new RefreshTokenRepository(DbContext);
        PasswordResetTokenRepository = new PasswordResetTokenRepository(DbContext);
        UnitOfWork = DbContext;

        JwtOptions = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            Issuer = "eticketing-identity",
            Audience = "eticketing-services",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14
        };
        TokenGenerator = new JwtTokenGenerator(Options.Create(JwtOptions));
    }


    /// <summary>The real outbox publisher over this fixture's own DbContext, for the tests that
    /// need to see an actual OutboxMessage row rather than a satisfied mock. Every other test uses
    /// the mock, which cannot tell a publish that writes a row from one that writes nothing.</summary>
    public IEventPublisher OutboxPublisher => new OutboxEventPublisher<IdentityDbContext>(DbContext);

    public IAuthService CreateAuthService(IEventPublisher? eventPublisher = null) => new AuthService(
        UserRepository, RefreshTokenRepository, PasswordResetTokenRepository, UnitOfWork, TokenGenerator,
        eventPublisher ?? EventPublisherMock.Object, Options.Create(JwtOptions));

    public IOrganizationService CreateOrganizationService(IEventPublisher? eventPublisher = null)
    {
        var publisher = eventPublisher ?? EventPublisherMock.Object;
        return new OrganizationService(
            OrganizationRepository, UserRepository, UnitOfWork, BlobStorage, publisher,
            CreateSnapshotPublisher(publisher));
    }

    /// <summary>The real snapshot publisher, not a mock: what it derives — specifically which
    /// address ends up in SuperAdminEmail — is the part that goes wrong, and a mock would assert
    /// only that somebody was asked to publish something.</summary>
    public IOrganizationSnapshotPublisher CreateSnapshotPublisher(IEventPublisher? eventPublisher = null) =>
        new OrganizationSnapshotPublisher(
            OrganizationRepository, UserRepository, eventPublisher ?? EventPublisherMock.Object, UnitOfWork);

    public IAdminService CreateAdminService() => new AdminService(
        UserRepository, RefreshTokenRepository, UnitOfWork, EventPublisherMock.Object);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
