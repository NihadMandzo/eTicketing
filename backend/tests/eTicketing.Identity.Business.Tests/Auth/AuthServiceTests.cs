using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace eTicketing.Identity.Business.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IAuthService _sut;

    public AuthServiceTests()
    {
        _sut = _fixture.CreateAuthService();
    }

    private static RegisterRequest ValidRegisterRequest(string email = "jane.doe@example.com", string username = "janedoe") => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        Username = username,
        Password = "SuperSecret123",
        PhoneNumber = null
    };

    [Fact]
    public async Task RegisterAsync_WithNewUser_IssuesAccessAndRefreshTokens()
    {
        var result = await _sut.RegisterAsync(ValidRegisterRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.User.RoleName.Should().Be("User");
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsConflict()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        var result = await _sut.RegisterAsync(ValidRegisterRequest(username: "someoneElse"));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_Succeeds()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());

        var result = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "SuperSecret123" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Email.Should().Be("jane.doe@example.com");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsUnauthorized()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());

        var result = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "WrongPassword" });

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ReturnsUnauthorized()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByEmailAsync("jane.doe@example.com");
        user!.IsActive = false;
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "SuperSecret123" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.inactive_account");
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesAndReturnsNewPair()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var originalRefreshToken = register.Value!.RefreshToken;

        var result = await _sut.RefreshAsync(originalRefreshToken);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBe(originalRefreshToken);
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshAsync_WithAlreadyRotatedToken_DetectsReuseAndRevokesWholeFamily()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var originalRefreshToken = register.Value!.RefreshToken;

        var rotated = await _sut.RefreshAsync(originalRefreshToken);
        var newRefreshToken = rotated.Value!.RefreshToken;

        // Replay the OLD (already-rotated-away) token — this is reuse.
        var reuseAttempt = await _sut.RefreshAsync(originalRefreshToken);

        reuseAttempt.IsFailure.Should().BeTrue();
        reuseAttempt.Error.Code.Should().Be("auth.refresh_reuse_detected");

        // The whole family (including the still-fresh rotated token) must now be dead too.
        var afterReuse = await _sut.RefreshAsync(newRefreshToken);
        afterReuse.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_Fails()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        // GetByTokenHashAsync is a no-tracking read (see RefreshTokenRepository), so mutating
        // it wouldn't persist — go through the tracked DbContext directly to force expiry.
        var storedToken = await _fixture.DbContext.RefreshTokens.SingleAsync(
            t => t.TokenHash == RefreshTokenGenerator.Hash(register.Value!.RefreshToken));
        storedToken.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.RefreshAsync(register.Value!.RefreshToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.refresh_expired");
    }

    [Fact]
    public async Task RefreshAsync_WithDeactivatedUser_ReturnsUnauthorized()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByEmailAsync("jane.doe@example.com");
        user!.IsActive = false;
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.RefreshAsync(register.Value!.RefreshToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.inactive_account");
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentRequestsWithSameToken_OnlyOneSucceeds()
    {
        // Two independent DbContexts/connections against the same shared-cache Sqlite database,
        // simulating two concurrent HTTP requests each with their own request-scoped DbContext —
        // as opposed to _sut/_fixture's single shared DbContext, which can't exercise a real race.
        var dbConnectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keepAliveConnection = new SqliteConnection(dbConnectionString);
        keepAliveConnection.Open();

        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(dbConnectionString)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            Issuer = "eticketing-identity",
            Audience = "eticketing-services",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14
        });
        var tokenGenerator = new JwtTokenGenerator(jwtOptions);

        static IAuthService BuildAuthService(IdentityDbContext ctx, IOptions<JwtOptions> opts, IJwtTokenGenerator generator) =>
            new AuthService(
                new UserRepository(ctx), new RefreshTokenRepository(ctx), ctx,
                generator, Mock.Of<IEventPublisher>(), opts);

        await using var seedContext = new IdentityDbContext(dbOptions);
        await seedContext.Database.EnsureCreatedAsync();

        var register = await BuildAuthService(seedContext, jwtOptions, tokenGenerator).RegisterAsync(ValidRegisterRequest());
        var refreshToken = register.Value!.RefreshToken;

        await using var contextA = new IdentityDbContext(dbOptions);
        await using var contextB = new IdentityDbContext(dbOptions);
        var serviceA = BuildAuthService(contextA, jwtOptions, tokenGenerator);
        var serviceB = BuildAuthService(contextB, jwtOptions, tokenGenerator);

        var taskA = serviceA.RefreshAsync(refreshToken);
        var taskB = serviceB.RefreshAsync(refreshToken);
        var completed = await Task.WhenAll(taskA, taskB);

        var results = completed;
        results.Count(r => r.IsSuccess).Should().Be(1, "only the request that wins the conditional revoke should mint a successor token");
        results.Count(r => r.IsFailure).Should().Be(1);
        // Depending on interleaving, the loser either finds 0 rows affected by its own
        // conditional revoke ("invalid_refresh_token") or — if it re-reads the token after the
        // winner already committed — observes it as already revoked ("refresh_reuse_detected").
        // Either way it must be a failure, and only one side may ever succeed.
        results.Single(r => r.IsFailure).Error.Code.Should().BeOneOf("auth.invalid_refresh_token", "auth.refresh_reuse_detected");
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownToken_Fails()
    {
        var result = await _sut.RefreshAsync("this-token-was-never-issued");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalid_refresh_token");
    }

    [Fact]
    public async Task LogoutAsync_RevokesTheRefreshToken_SoItCanNoLongerBeUsed()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());

        await _sut.LogoutAsync(register.Value!.RefreshToken);
        var afterLogout = await _sut.RefreshAsync(register.Value.RefreshToken);

        afterLogout.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsValidationError()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());

        var result = await _sut.ChangePasswordAsync(register.Value!.User.Id, new ChangePasswordRequest
        {
            CurrentPassword = "NotTheRealPassword",
            NewPassword = "BrandNewPassword123",
            ConfirmPassword = "BrandNewPassword123"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.wrong_current_password");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_AllowsSubsequentLoginWithNewPassword()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());

        var changeResult = await _sut.ChangePasswordAsync(register.Value!.User.Id, new ChangePasswordRequest
        {
            CurrentPassword = "SuperSecret123",
            NewPassword = "BrandNewPassword123",
            ConfirmPassword = "BrandNewPassword123"
        });

        changeResult.IsSuccess.Should().BeTrue();

        var login = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "BrandNewPassword123" });
        login.IsSuccess.Should().BeTrue();
    }

    public void Dispose() => _fixture.Dispose();
}
