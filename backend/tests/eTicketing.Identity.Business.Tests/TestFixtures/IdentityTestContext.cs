using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

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

    public IAuthService CreateAuthService() => new AuthService(
        UserRepository, RefreshTokenRepository, UnitOfWork, TokenGenerator,
        EventPublisherMock.Object, Options.Create(JwtOptions));

    public IOrganizationService CreateOrganizationService() => new OrganizationService(
        OrganizationRepository, UserRepository, UnitOfWork, BlobStorage);

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
    }
}
