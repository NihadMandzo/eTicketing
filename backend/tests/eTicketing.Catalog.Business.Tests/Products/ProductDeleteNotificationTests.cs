using System.Security.Claims;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Moq;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// Catalog's half of "the buyers hear about a cancellation": it deletes the product and announces
/// it, but knows nothing about tickets. The fan-out into per-recipient emails is
/// eTicketing.Ticketing's job — see ProductDeletionNotifierTests over there.
/// </summary>
public class ProductDeleteNotificationTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly Guid _organizationId = Guid.NewGuid();
    private Category _music = null!;

    public ProductDeleteNotificationTests()
    {
        _sut = _fixture.CreateProductService();
        SeedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task DeleteAsync_PublishesProductDeletedCarryingWhatTheEmailsNeed()
    {
        var id = await CreateAsync();

        var result = await _sut.DeleteAsync(id, Organizer());

        result.IsSuccess.Should().BeTrue();
        VerifyPublished(e =>
            e.ProductId == id
            && e.ProductName == "Ljetni Festival"
            && e.OrganizationId == _organizationId
            && e.ProductDate == ProductDate);
    }

    [Fact]
    public async Task DeleteAsync_ByTheOwningOrganizer_DoesNotFlagItAsAPlatformDeletion()
    {
        // The organizer just deleted their own product — mailing them about it would be absurd.
        var id = await CreateAsync();

        await _sut.DeleteAsync(id, Organizer());

        VerifyPublished(e => !e.DeletedByPlatformStaff);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    public async Task DeleteAsync_ByPlatformStaff_FlagsItSoTheOrganizationIsTold(string role)
    {
        var id = await CreateAsync();

        await _sut.DeleteAsync(id, BuildCaller(role));

        VerifyPublished(e => e.DeletedByPlatformStaff);
    }

    [Fact]
    public async Task DeleteAsync_OfADraft_StillPublishes()
    {
        // A draft has no buyers, so no buyer email follows — but when platform staff removes one,
        // the organization that was still preparing it would otherwise never find out.
        var id = await CreateAsync();

        await _sut.DeleteAsync(id, BuildCaller("SuperAdmin"));

        VerifyPublished(e => e.ProductId == id);
    }

    [Fact]
    public async Task DeleteAsync_ForAnotherOrganizationsProduct_PublishesNothing()
    {
        var id = await CreateAsync();

        var result = await _sut.DeleteAsync(id, BuildCaller("OrganizationSuperAdmin", Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        VerifyNothingPublished();
    }

    [Fact]
    public async Task DeleteAsync_ForAnUnknownProduct_PublishesNothing()
    {
        var result = await _sut.DeleteAsync(Guid.NewGuid(), Organizer());

        result.IsSuccess.Should().BeFalse();
        VerifyNothingPublished();
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────

    private static readonly DateTime ProductDate =
        new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 20, 0, 0, DateTimeKind.Utc)
            .AddDays(30);

    private void VerifyPublished(Func<ProductDeleted, bool> predicate) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductDeleted, It.Is<ProductDeleted>(e => predicate(e)), It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyNothingPublished() =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductDeleted, It.IsAny<ProductDeleted>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private async Task<Guid> CreateAsync()
    {
        var created = await _sut.CreateAsync(Request(), Organizer());
        created.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.Invocations.Clear();
        return created.Value!.Id;
    }

    private UpsertProductRequest Request() => new()
    {
        Name = "Ljetni Festival",
        Description = "Opis festivala",
        Date = ProductDate,
        CategoryId = _music.Id,
        Latitude = 43.8563,
        Longitude = 18.4131,
        City = City.Sarajevo,
    };

    private async Task SeedAsync()
    {
        _music = new Category { Name = "Pozorište", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    private ClaimsPrincipal Organizer() => BuildCaller("OrganizationSuperAdmin", _organizationId);

    private static ClaimsPrincipal BuildCaller(string role, Guid? organizationId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };
        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
