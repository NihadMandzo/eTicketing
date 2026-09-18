using eTicketing.Contracts.Events;
using eTicketing.Contracts.Messaging;
using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Business.Tests.Auth;

/// <summary>
/// Registration, against the real outbox publisher instead of the mock every other test here uses.
///
/// The mock cannot see the thing that matters: it is satisfied by a publish placed *after* the
/// save, which with an outbox writes a row nothing ever commits and silently produces no event at
/// all. These assert the row is actually there afterwards.
/// </summary>
public class AuthServiceOutboxTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IAuthService _sut;

    public AuthServiceOutboxTests()
    {
        _sut = _fixture.CreateAuthService(_fixture.OutboxPublisher);
    }

    private static RegisterRequest ValidRequest() => new()
    {
        FirstName = "Amila",
        LastName = "Hodžić",
        Email = "amila@example.com",
        Username = "amila",
        Password = "SuperSecret123",
        PhoneNumber = "061234567",
    };

    [Fact]
    public async Task RegisterAsync_CommitsTheVerificationEmailEventWithTheAccount()
    {
        var result = await _sut.RegisterAsync(ValidRequest());

        result.IsSuccess.Should().BeTrue();

        _fixture.DbContext.ChangeTracker.Clear();
        var message = await _fixture.DbContext.Set<OutboxMessage>().AsNoTracking().SingleAsync();
        message.RoutingKey.Should().Be(EventNames.VerificationEmailRequested);

        // The account and the event it owes are one transaction now. Before the outbox this
        // publish happened after IssueTokensAsync's save, so a process that died in between left a
        // registered user who was never sent a verification code and could never verify.
        (await _fixture.DbContext.Users.AsNoTracking().CountAsync(u => u.Email == "amila@example.com"))
            .Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_WhenTheEmailIsTaken_WritesNoEvent()
    {
        await _sut.RegisterAsync(ValidRequest());
        _fixture.DbContext.ChangeTracker.Clear();

        var second = await _sut.RegisterAsync(ValidRequest());

        second.IsFailure.Should().BeTrue();

        // Still one, from the first registration — the rejected attempt added nothing. A refused
        // request must not produce an event for an account that does not exist.
        _fixture.DbContext.ChangeTracker.Clear();
        (await _fixture.DbContext.Set<OutboxMessage>().AsNoTracking().CountAsync()).Should().Be(1);
    }

    public void Dispose() => _fixture.Dispose();
}
