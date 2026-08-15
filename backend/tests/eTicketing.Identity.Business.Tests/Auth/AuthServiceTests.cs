using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Security;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
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
                new UserRepository(ctx), new RefreshTokenRepository(ctx), new PasswordResetTokenRepository(ctx), ctx,
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

    [Fact]
    public async Task RegisterAsync_WithNewUser_PersistsVerificationCodeAndExpiry()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());

        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        user!.EmailVerificationCode.Should().NotBeNullOrWhiteSpace();
        user.EmailVerificationCodeExpiresAt.Should().NotBeNull();
        user.EmailVerificationCodeExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithCorrectCode_MarksEmailVerified()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);

        var result = await _sut.VerifyEmailAsync(user!.Id, new VerifyEmailRequest { Code = user.EmailVerificationCode! });

        result.IsSuccess.Should().BeTrue();
        var updated = await _fixture.UserRepository.GetByIdAsync(user.Id);
        updated!.IsEmailVerified.Should().BeTrue();
        updated.EmailVerificationCode.Should().BeNull();
        updated.EmailVerificationCodeExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithExpiredCode_ReturnsValidationError()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        user!.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddHours(-1);
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = user.EmailVerificationCode! });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.verification_code_expired");
    }

    [Fact]
    public async Task VerifyEmailAsync_WithWrongCode_ReturnsValidationError()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());

        var result = await _sut.VerifyEmailAsync(register.Value!.User.Id, new VerifyEmailRequest { Code = "WRONG1" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalid_verification_code");
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyVerified_ReturnsConflict()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        var code = user!.EmailVerificationCode!;
        (await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = code })).IsSuccess.Should().BeTrue();

        var result = await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = code });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.email_already_verified");
    }

    [Fact]
    public async Task VerifyEmailAsync_CalledTwiceWithSameCode_SecondAttemptFails()
    {
        // Documents "verification code cannot be reused": the code is cleared on the first
        // success, so a replay of the same code hits the expired/missing branch, not a second
        // "already verified" outcome from a code standpoint.
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        var code = user!.EmailVerificationCode!;

        var first = await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = code });
        first.IsSuccess.Should().BeTrue();

        var second = await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = code });
        second.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_GeneratesNewCodeAndPublishesEvent()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        var originalCode = user!.EmailVerificationCode;

        var result = await _sut.ResendVerificationEmailAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        var updated = await _fixture.UserRepository.GetByIdAsync(user.Id);
        updated!.EmailVerificationCode.Should().NotBe(originalCode);
        _fixture.EventPublisherMock.Verify(p => p.PublishAsync(
            EventNames.VerificationEmailRequested,
            It.Is<VerificationEmailRequested>(e => e.UserId == user.Id && e.VerificationCode == updated.EmailVerificationCode),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_OldCodeNoLongerValidAfterResend()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        var oldCode = user!.EmailVerificationCode!;

        await _sut.ResendVerificationEmailAsync(user.Id);

        var result = await _sut.VerifyEmailAsync(user.Id, new VerifyEmailRequest { Code = oldCode });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalid_verification_code");
    }

    [Fact]
    public async Task ForgotPasswordAsync_ForUserRole_PublishesPasswordResetRequestedEvent()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisherMock.Verify(p => p.PublishAsync(
            EventNames.PasswordResetRequested,
            It.Is<PasswordResetRequested>(e => e.Email == "jane.doe@example.com" && !string.IsNullOrWhiteSpace(e.ResetToken)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ForStaffRole_ReturnsValidationError()
    {
        await SeedStaffUserAsync(RoleType.OrganizationAdmin, "staff@example.com");

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "staff@example.com" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.forgot_password_staff_not_allowed");
        _fixture.EventPublisherMock.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPasswordAsync_ForUnknownEmail_ReturnsSuccessWithoutPublishing()
    {
        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "nobody@example.com" });

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisherMock.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPasswordAsync_CalledTwiceForSameUser_InvalidatesPreviousToken()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });
        var firstToken = GetLastPublishedResetToken();

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });

        var usingOldToken = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = firstToken,
            NewPassword = "AnotherNewPassword123",
            ConfirmPassword = "AnotherNewPassword123"
        });

        usingOldToken.IsFailure.Should().BeTrue();
        usingOldToken.Error.Code.Should().Be("auth.invalid_reset_token");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_ChangesPassword()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });
        var rawToken = GetLastPublishedResetToken();

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "AnotherNewPassword123",
            ConfirmPassword = "AnotherNewPassword123"
        });

        result.IsSuccess.Should().BeTrue();
        var login = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "AnotherNewPassword123" });
        login.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_OnSuccess_RevokesAllActiveRefreshTokens()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });
        var rawToken = GetLastPublishedResetToken();

        await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "AnotherNewPassword123",
            ConfirmPassword = "AnotherNewPassword123"
        });

        var refreshAfterReset = await _sut.RefreshAsync(register.Value!.RefreshToken);
        refreshAfterReset.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ReturnsUnauthorized()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });
        var rawToken = GetLastPublishedResetToken();
        var stored = await _fixture.DbContext.PasswordResetTokens.SingleAsync(
            t => t.TokenHash == RefreshTokenGenerator.Hash(rawToken));
        stored.ExpiresAt = DateTime.UtcNow.AddHours(-1);
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "AnotherNewPassword123",
            ConfirmPassword = "AnotherNewPassword123"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalid_reset_token");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithAlreadyUsedToken_ReturnsUnauthorized()
    {
        await _sut.RegisterAsync(ValidRegisterRequest());
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "jane.doe@example.com" });
        var rawToken = GetLastPublishedResetToken();
        await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "AnotherNewPassword123",
            ConfirmPassword = "AnotherNewPassword123"
        });

        // Replaying the exact same (now-used) token is the "reset token cannot be reused" case.
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "YetAnotherPassword123",
            ConfirmPassword = "YetAnotherPassword123"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalid_reset_token");
    }

    [Fact]
    public async Task SetNewPasswordAsync_ClearsMustChangePasswordAndRevokesSessions()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest());
        var user = await _fixture.UserRepository.GetByIdAsync(register.Value!.User.Id);
        user!.MustChangePassword = true;
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.SetNewPasswordAsync(user.Id, new SetNewPasswordRequest
        {
            NewPassword = "SelfChosenPassword123",
            ConfirmPassword = "SelfChosenPassword123"
        });

        result.IsSuccess.Should().BeTrue();
        var updated = await _fixture.UserRepository.GetByIdAsync(user.Id);
        updated!.MustChangePassword.Should().BeFalse();

        var refreshAfter = await _sut.RefreshAsync(register.Value.RefreshToken);
        refreshAfter.IsFailure.Should().BeTrue();

        var login = await _sut.LoginAsync(new LoginRequest { EmailOrUsername = "janedoe", Password = "SelfChosenPassword123" });
        login.IsSuccess.Should().BeTrue();
    }

    private async Task<User> SeedStaffUserAsync(RoleType role, string email)
    {
        var (hash, salt) = PasswordHasher.Hash("SomePassword123");
        var user = new User
        {
            FirstName = "Staff",
            LastName = "User",
            Email = email,
            Username = email.Split('@')[0],
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false
        };
        await _fixture.UserRepository.AddAsync(user);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return user;
    }

    /// <summary>Pulls the raw reset token out of the most recent PasswordResetRequested publish
    /// captured by the mock — the only place the raw value ever exists outside the (unrecoverable,
    /// by design) hash stored in the DB.</summary>
    private string GetLastPublishedResetToken() => _fixture.EventPublisherMock.Invocations
        .Where(i => i.Method.Name == nameof(IEventPublisher.PublishAsync) && i.Arguments[0] as string == EventNames.PasswordResetRequested)
        .Select(i => ((PasswordResetRequested)i.Arguments[1]).ResetToken)
        .Last();

    public void Dispose() => _fixture.Dispose();
}
