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
/// The Catalog half of "if anything vital changes, every buyer hears about it": detect the diff and
/// put it on the bus. Catalog has no idea who the buyers are — eTicketing.Ticketing consumes
/// product.updated and fans it out — so everything asserted here is about WHEN an event is
/// published and WHAT it says, never about recipients.
/// </summary>
public class ProductUpdateNotificationTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private Category _music = null!;
    private Category _sport = null!;

    public ProductUpdateNotificationTests()
    {
        _sut = _fixture.CreateProductService();
        SeedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task UpdateAsync_OnAPublishedProductWithAChangedDate_PublishesProductUpdated()
    {
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request() with { Date = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc) }, Caller());

        VerifyPublished(e =>
            e.ProductId == id
            && e.Changes.Count == 1
            && e.Changes[0].Field == "Datum i vrijeme"
            && e.Changes[0].OldValue == "01.09.2026. 20:00"
            && e.Changes[0].NewValue == "02.09.2026. 20:00");
    }

    [Fact]
    public async Task UpdateAsync_WithSeveralChangedFields_ReportsEachOne()
    {
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(
            id,
            Request() with { Name = "Novi naziv", City = City.Mostar, Description = "Novi opis" },
            Caller());

        VerifyPublished(e => e.Changes.Select(c => c.Field).OrderBy(f => f)
            .SequenceEqual(new[] { "Grad", "Naziv", "Opis" }));
    }

    [Fact]
    public async Task UpdateAsync_WithAMovedMapPin_ReportsItAsOneCombinedChange()
    {
        // Latitude and longitude mean nothing apart — "43.85630, 18.41310 → 43.34380, 17.80780"
        // reads as a moved venue; two rows of raw decimals do not.
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request() with { Latitude = 43.3438, Longitude = 17.8078 }, Caller());

        VerifyPublished(e =>
            e.Changes.Count == 1
            && e.Changes[0].Field == "Lokacija na mapi"
            && e.Changes[0].NewValue == "43.34380, 17.80780");
    }

    [Fact]
    public async Task UpdateAsync_WithAChangedCategory_ReportsTheCategoryNamesNotIds()
    {
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request() with { CategoryId = _sport.Id }, Caller());

        VerifyPublished(e =>
            e.Changes.Any(c => c.Field == "Kategorija" && c.OldValue == "Muzika" && c.NewValue == "Sport"));
    }

    [Fact]
    public async Task UpdateAsync_SavingIdenticalValues_PublishesNothing()
    {
        // Opening the edit form and pressing save must not mail everyone who bought a ticket.
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request(), Caller());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task UpdateAsync_OnADraftProduct_PublishesNothing()
    {
        // A draft has no buyers by construction, so there is nobody to notify.
        var created = await _sut.CreateAsync(Request(), Caller());

        await _sut.UpdateAsync(created.Value!.Id, Request() with { Name = "Izmijenjeno" }, Caller());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task UpdateAsync_WhenValidationFails_PublishesNothing()
    {
        var id = await CreatePublishedAsync();

        var result = await _sut.UpdateAsync(id, Request() with { CategoryId = 9999 }, Caller());

        result.IsFailure.Should().BeTrue();
        VerifyNothingPublished();
    }

    [Fact]
    public async Task UpdateAsync_ForAnotherOrganizationsProduct_PublishesNothing()
    {
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request() with { Name = "Izmijenjeno" }, BuildCaller("OrganizationSuperAdmin", Guid.NewGuid()));

        VerifyNothingPublished();
    }

    [Fact]
    public async Task PublishAsync_OnItsOwn_PublishesNothing()
    {
        // Draft → Published is not a change to an already-live product; the buyers it would notify
        // cannot exist yet.
        var created = await _sut.CreateAsync(Request(), Caller());

        await _sut.PublishAsync(created.Value!.Id, Caller());

        VerifyNothingPublished();
    }

    [Fact]
    public async Task UpdateAsync_CarriesTheNewProductName_SoTheEmailSubjectMatchesTheCurrentTitle()
    {
        var id = await CreatePublishedAsync();

        await _sut.UpdateAsync(id, Request() with { Name = "Novi naziv" }, Caller());

        VerifyPublished(e => e.ProductName == "Novi naziv");
    }

    private void VerifyPublished(Func<ProductUpdated, bool> predicate) =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductUpdated, It.Is<ProductUpdated>(e => predicate(e)), It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyNothingPublished() =>
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.ProductUpdated, It.IsAny<ProductUpdated>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private async Task<Guid> CreatePublishedAsync()
    {
        var created = await _sut.CreateAsync(Request(), Caller());
        await _sut.PublishAsync(created.Value!.Id, Caller());
        // Reset so assertions only see what the update under test published.
        _fixture.EventPublisher.Invocations.Clear();
        return created.Value.Id;
    }

    private UpsertProductRequest Request() => new()
    {
        Name = "Ljetni Festival",
        Description = "Opis festivala",
        Date = new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc),
        CategoryId = _music.Id,
        Latitude = 43.8563,
        Longitude = 18.4131,
        City = City.Sarajevo,
    };

    private async Task SeedAsync()
    {
        _music = new Category { Name = "Muzika", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _sport = new Category { Name = "Sport", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.CategoryRepository.AddAsync(_sport);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    private ClaimsPrincipal Caller() => BuildCaller("OrganizationSuperAdmin", _orgA);

    private static ClaimsPrincipal BuildCaller(string role, Guid organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new("organizationId", organizationId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public void Dispose() => _fixture.Dispose();
}
