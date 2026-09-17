using System.Security.Claims;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Messaging;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Tests.TestFixtures;
using eTicketing.Identity.Data.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Business.Tests.Organizations;

/// <summary>
/// Identity's side of the organization read model that replaced the Ticketing→Identity HTTP call.
///
/// <para>The one that actually bites is <c>SuperAdminEmail</c>. It is not a column on Organization —
/// it is derived from the Users table — so it can move without any organization row changing at all,
/// and when it goes stale the symptom is a cancellation email in another service telling a buyer to
/// chase a refund at an address nobody reads. Several of the tests below exist purely to pin the
/// paths where that can happen.</para>
/// </summary>
public class OrganizationSnapshotPublishingTests : IDisposable
{
    private readonly IdentityTestContext _fixture = new();
    private readonly IOrganizationService _sut;

    public OrganizationSnapshotPublishingTests()
    {
        _sut = _fixture.CreateOrganizationService();
    }

    private static CreateOrganizationRequest CreateRequest(string adminEmail = "alice@acme.example.com") => new()
    {
        Name = "Acme Events",
        Description = "We throw great events.",
        Address = "Ferhadija 1",
        PhoneNumber = "+387 61 000 000",
        Email = "info@acme.example.com",
        Website = "https://acme.example.com",
        AdminFirstName = "Alice",
        AdminLastName = "Admin",
        AdminEmail = adminEmail,
        AdminUsername = "aliceadmin",
        AdminPassword = "SuperSecret123",
    };

    private static ClaimsPrincipal PlatformStaffCaller() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, nameof(RoleType.SuperAdmin)),
        ], "TestAuth"));

    /// <summary>The single snapshot the call under test published. Fails loudly when there is none
    /// — "published nothing" is the failure these tests exist to catch.</summary>
    private OrganizationSnapshotChanged CapturedSnapshot()
    {
        var published = _fixture.EventPublisherMock.Invocations
            .Where(i => (string)i.Arguments[0] == EventNames.OrganizationSnapshotChanged)
            .Select(i => (OrganizationSnapshotChanged)i.Arguments[1])
            .ToList();

        published.Should().ContainSingle("the change under test must publish exactly one snapshot");
        return published[0];
    }

    private List<OrganizationSnapshotChanged> PublishedSnapshots() =>
        _fixture.EventPublisherMock.Invocations
            .Where(i => (string)i.Arguments[0] == EventNames.OrganizationSnapshotChanged)
            .Select(i => (OrganizationSnapshotChanged)i.Arguments[1])
            .ToList();

    private void VerifyNoSnapshotPublished() =>
        _fixture.EventPublisherMock.Invocations
            .Where(i => (string)i.Arguments[0] == EventNames.OrganizationSnapshotChanged)
            .Should().BeEmpty();

    // ── Organization lifecycle ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PublishesASnapshotCarryingTheFoundingAdminAsSuperAdmin()
    {
        // The founding admin is the OrganizationSuperAdmin by construction, and is in the change
        // tracker rather than the database when this publishes — querying for the address here
        // returns null, which would leave Ticketing with no refund contact until the next sweep.
        var result = await _sut.CreateAsync(CreateRequest());

        var snapshot = CapturedSnapshot();
        snapshot.OrganizationId.Should().Be(result.Value!.Id);
        snapshot.Name.Should().Be("Acme Events");
        snapshot.Address.Should().Be("Ferhadija 1");
        snapshot.Email.Should().Be("info@acme.example.com");
        snapshot.PhoneNumber.Should().Be("+387 61 000 000");
        snapshot.SuperAdminEmail.Should().Be("alice@acme.example.com");
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_CommitsTheSnapshotInTheSameTransactionAsTheOrganization()
    {
        // Against the real outbox publisher rather than a mock: an event announcing an organization
        // whose INSERT then failed would leave another service holding a row for something that
        // does not exist.
        var service = _fixture.CreateOrganizationService(_fixture.OutboxPublisher);

        var result = await service.CreateAsync(CreateRequest());

        var rows = await _fixture.DbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.RoutingKey == EventNames.OrganizationSnapshotChanged).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Payload.Should().Contain(result.Value!.Id.ToString());
    }

    [Fact]
    public async Task UpdateAsync_PublishesTheEditedDetails()
    {
        var created = await _sut.CreateAsync(CreateRequest());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.UpdateAsync(created.Value!.Id, new UpdateOrganizationRequest
        {
            Name = "Acme Events BH",
            Description = "Novi opis",
            Address = "Titova 5",
            PhoneNumber = "+387 33 111 222",
            Email = "kontakt@acme.example.com",
            Website = "https://acme.example.com",
            IsActive = false,
        });

        var snapshot = CapturedSnapshot();
        snapshot.Name.Should().Be("Acme Events BH");
        snapshot.Address.Should().Be("Titova 5");
        snapshot.Email.Should().Be("kontakt@acme.example.com");
        snapshot.PhoneNumber.Should().Be("+387 33 111 222");
        snapshot.IsActive.Should().BeFalse();
        // Still resolved from the Users table, not dropped just because this request cannot change it.
        snapshot.SuperAdminEmail.Should().Be("alice@acme.example.com");
    }

    [Fact]
    public async Task UpdateAsync_ForAnUnknownOrganization_PublishesNothing()
    {
        await _sut.UpdateAsync(Guid.NewGuid(), new UpdateOrganizationRequest
        {
            Name = "Nepostojeća",
            Address = "Nigdje 1",
            PhoneNumber = "+387 33 000 000",
            Email = "x@x.ba",
            IsActive = true,
        });

        VerifyNoSnapshotPublished();
    }

    [Fact]
    public async Task DeleteAsync_PublishesOrganizationDeleted()
    {
        var created = await _sut.CreateAsync(CreateRequest());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.DeleteAsync(created.Value!.Id);

        _fixture.EventPublisherMock.Invocations
            .Where(i => (string)i.Arguments[0] == EventNames.OrganizationDeleted)
            .Select(i => ((OrganizationDeleted)i.Arguments[1]).OrganizationId)
            .Should().Equal(created.Value.Id);
    }

    // ── The half that is not on the Organization row at all ──────────────────────────────────

    [Fact]
    public async Task UpdateUserAsync_ChangingTheSuperAdminsEmail_PublishesTheNewAddress()
    {
        // The reason this class exists. Nothing on the Organization row changed, and the refund
        // contact printed on every cancellation email just moved. Note the assertion is on the NEW
        // address specifically: reading it back from the database here — before the caller's save —
        // would return the old one and pass a weaker test while shipping the bug.
        var created = await _sut.CreateAsync(CreateRequest());
        // Scoped to this organization: the fixture seeds others, each with a super admin of its own.
        var superAdmin = await _fixture.DbContext.Users
            .SingleAsync(u => u.OrganizationId == created.Value!.Id && u.Role == RoleType.OrganizationSuperAdmin);
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.UpdateUserAsync(created.Value!.Id, superAdmin.Id, new UpdateOrganizationUserRequest
        {
            FirstName = "Alice",
            LastName = "Admin",
            Email = "alice.nova@acme.example.com",
            Username = "aliceadmin",
        }, PlatformStaffCaller());

        CapturedSnapshot().SuperAdminEmail.Should().Be("alice.nova@acme.example.com");
    }

    [Fact]
    public async Task UpdateUserAsync_OnAnOrganizationAdmin_PublishesNothing()
    {
        // Their address is on no snapshot, so republishing for them would put a row on the bus per
        // staff edit for nothing.
        var created = await _sut.CreateAsync(CreateRequest());
        var added = await _sut.AddUserAsync(created.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Admin",
            Email = "bob@acme.example.com",
            Username = "bobadmin",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin,
        }, PlatformStaffCaller());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.UpdateUserAsync(created.Value.Id, added.Value!.Id, new UpdateOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Adminović",
            Email = "bob@acme.example.com",
            Username = "bobadmin",
        }, PlatformStaffCaller());

        VerifyNoSnapshotPublished();
    }

    [Fact]
    public async Task AddUserAsync_ForAnOrganizationAdmin_PublishesNothing()
    {
        var created = await _sut.CreateAsync(CreateRequest());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.AddUserAsync(created.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Admin",
            Email = "bob@acme.example.com",
            Username = "bobadmin",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin,
        }, PlatformStaffCaller());

        VerifyNoSnapshotPublished();
    }

    [Fact]
    public async Task RemoveUserAsync_PublishesNothing()
    {
        // Removing an OrganizationSuperAdmin is refused outright (exactly one per organization,
        // always), so a removal can never move SuperAdminEmail — which is why there is no republish
        // on this path, and why that absence is worth pinning rather than leaving to be re-derived.
        var created = await _sut.CreateAsync(CreateRequest());
        var added = await _sut.AddUserAsync(created.Value!.Id, new AddOrganizationUserRequest
        {
            FirstName = "Bob",
            LastName = "Admin",
            Email = "bob@acme.example.com",
            Username = "bobadmin",
            Password = "SuperSecret123",
            Role = RoleType.OrganizationAdmin,
        }, PlatformStaffCaller());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _sut.RemoveUserAsync(created.Value.Id, added.Value!.Id, PlatformStaffCaller());

        VerifyNoSnapshotPublished();
    }

    // ── The republish sweep ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RepublishAllAsync_PublishesOneEventPerOrganizationWithItsSuperAdminAddress()
    {
        // The backfill and the self-heal in one. It is pushed from this side because the
        // synchronous call the other service used to pull with is gone — so if this sweep skips an
        // organization, nothing else will ever fill it in.
        var created = await _sut.CreateAsync(CreateRequest());
        var organizationCount = await _fixture.DbContext.Organizations.CountAsync();
        _fixture.EventPublisherMock.Invocations.Clear();

        var published = await _fixture.CreateSnapshotPublisher().RepublishAllAsync();

        published.Should().Be(organizationCount);
        PublishedSnapshots().Should().HaveCount(organizationCount);
        PublishedSnapshots().Single(s => s.OrganizationId == created.Value!.Id)
            .SuperAdminEmail.Should().Be("alice@acme.example.com");
    }

    [Fact]
    public async Task RepublishAllAsync_PublishesTheWholeSweepUnderOneTimestamp()
    {
        // Not a cosmetic detail: the projection on the other side drops anything older than the row
        // it holds, so a sweep whose rows disagreed about "now" would have members of the same
        // sweep racing each other.
        await _sut.CreateAsync(CreateRequest());
        _fixture.EventPublisherMock.Invocations.Clear();

        await _fixture.CreateSnapshotPublisher().RepublishAllAsync();

        PublishedSnapshots().Select(s => s.ChangedAt).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task RepublishAllAsync_CommitsItsOwnOutboxRows()
    {
        // Unlike every other publish here, this one has no caller transaction to join — without its
        // own save the sweep would add rows nothing persists and report a count of events it never
        // actually published.
        var organizationCount = await _fixture.DbContext.Organizations.CountAsync();
        var publisher = _fixture.CreateSnapshotPublisher(_fixture.OutboxPublisher);

        await publisher.RepublishAllAsync();

        var rows = await _fixture.DbContext.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.RoutingKey == EventNames.OrganizationSnapshotChanged).ToListAsync();
        rows.Should().HaveCount(organizationCount);
    }

    public void Dispose() => _fixture.Dispose();
}
