using eTicketing.Contracts.Events;
using eTicketing.Ticketing.Business.ReadModels;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Business.Tests.ReadModels;

/// <summary>
/// The Ticketing side of the organization read model that replaced the synchronous
/// Ticketing→Identity call.
///
/// <para>Unlike the product projection, a wrong row here never blocks a sale — it mislabels a report
/// row or misaddresses a cancellation email. That is a lower grade of wrong, but it fails silently,
/// which is why the ordering guard is tested rather than assumed: Identity republishes every
/// organization on a six-hourly sweep, so an old republish overtaking a fresh edit is the expected
/// traffic pattern here, not a hypothetical.</para>
/// </summary>
public class OrganizationSnapshotProjectorTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IOrganizationSnapshotProjector _sut;

    private readonly Guid _organizationId = Guid.NewGuid();
    private static readonly DateTime Noon = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    public OrganizationSnapshotProjectorTests()
    {
        _sut = _fixture.CreateOrganizationSnapshotProjector();
    }

    private OrganizationSnapshotChanged Event(
        string name = "Sarajevo Events",
        string address = "Ferhadija 1",
        string email = "kontakt@sarajevo-events.ba",
        string phoneNumber = "+387 33 123 456",
        string? superAdminEmail = "emir@sarajevo-events.ba",
        bool isActive = true,
        DateTime? changedAt = null) =>
        new(_organizationId, name, address, email, phoneNumber, superAdminEmail, isActive, changedAt ?? Noon);

    private Task<OrganizationSnapshot?> StoredAsync() =>
        _fixture.DbContext.OrganizationSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrganizationId == _organizationId);

    [Fact]
    public async Task ApplyAsync_ForAnUnknownOrganization_InsertsTheRow()
    {
        await _sut.ApplyAsync(Event());

        var stored = await StoredAsync();
        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Sarajevo Events");
        stored.Address.Should().Be("Ferhadija 1");
        stored.Email.Should().Be("kontakt@sarajevo-events.ba");
        stored.PhoneNumber.Should().Be("+387 33 123 456");
        stored.SuperAdminEmail.Should().Be("emir@sarajevo-events.ba");
        stored.IsActive.Should().BeTrue();
        stored.ChangedAt.Should().Be(Noon);
    }

    [Fact]
    public async Task ApplyAsync_ForAKnownOrganization_OverwritesRatherThanInserting()
    {
        await _sut.ApplyAsync(Event(name: "Stari naziv"));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(name: "Sarajevo Events", changedAt: Noon.AddMinutes(1)));

        (await _fixture.DbContext.OrganizationSnapshots.AsNoTracking().CountAsync()).Should().Be(1);
        (await StoredAsync())!.Name.Should().Be("Sarajevo Events");
    }

    [Fact]
    public async Task ApplyAsync_ForTheSameEventTwice_IsIdempotent()
    {
        // At-least-once delivery, and a six-hourly republish that resends the same state on purpose.
        var message = Event();

        await _sut.ApplyAsync(message);
        _fixture.DbContext.ChangeTracker.Clear();
        await _sut.ApplyAsync(message);

        (await _fixture.DbContext.OrganizationSnapshots.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_ForAnOlderEventThanTheRowItHolds_IgnoresIt()
    {
        // A republish sweep queued behind a live edit. Applying it would put the previous refund
        // contact back with nothing to show that it happened.
        await _sut.ApplyAsync(Event(superAdminEmail: "emir.novi@sarajevo-events.ba", changedAt: Noon));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(superAdminEmail: "emir.stari@sarajevo-events.ba", changedAt: Noon.AddHours(-6)));

        (await StoredAsync())!.SuperAdminEmail.Should().Be("emir.novi@sarajevo-events.ba");
    }

    [Fact]
    public async Task ApplyAsync_TracksASuperAdminEmailChangeWithNothingElseChanged()
    {
        // The whole reason Identity republishes on user changes at all: SuperAdminEmail is derived
        // from its Users table, so it moves while every other field stays exactly as it was.
        await _sut.ApplyAsync(Event(superAdminEmail: "emir@sarajevo-events.ba"));
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(superAdminEmail: "amila@sarajevo-events.ba", changedAt: Noon.AddMinutes(1)));

        var stored = await StoredAsync();
        stored!.SuperAdminEmail.Should().Be("amila@sarajevo-events.ba");
        stored.Name.Should().Be("Sarajevo Events");
    }

    [Fact]
    public async Task ApplyAsync_ForAnOrganizationWithNoSuperAdmin_StoresNull()
    {
        // Defensive rather than expected: exactly one OrganizationSuperAdmin per organization is
        // enforced, and a null here makes the cancellation notice fall back to the organization's
        // own address instead of throwing.
        await _sut.ApplyAsync(Event(superAdminEmail: null));

        (await StoredAsync())!.SuperAdminEmail.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_HardDeletesTheRow()
    {
        await _sut.ApplyAsync(Event());
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.RemoveAsync(_organizationId);

        (await StoredAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ForAnOrganizationItNeverProjected_DoesNothing()
    {
        var act = async () => await _sut.RemoveAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_ThenAnOlderChangedEvent_ReprojectsRatherThanStayingEmpty()
    {
        // Delete and re-create with the same id is not a real scenario, but a delete arriving before
        // a changed event that was already in flight is. There is no tombstone, so the row comes
        // back — which is the honest outcome: the alternative is a permanently unlabelable
        // organization after one out-of-order pair.
        await _sut.ApplyAsync(Event(changedAt: Noon));
        _fixture.DbContext.ChangeTracker.Clear();
        await _sut.RemoveAsync(_organizationId);
        _fixture.DbContext.ChangeTracker.Clear();

        await _sut.ApplyAsync(Event(changedAt: Noon.AddMinutes(-1)));

        (await StoredAsync()).Should().NotBeNull();
    }

    public void Dispose() => _fixture.Dispose();
}
