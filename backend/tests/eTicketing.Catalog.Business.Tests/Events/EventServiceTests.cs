using eTicketing.Catalog.Business.Events;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Events;

public class EventServiceTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IEventService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private Category _music = null!;
    private Category _sport = null!;

    public EventServiceTests()
    {
        _sut = _fixture.CreateEventService();
        SeedCategoriesAndEvents().GetAwaiter().GetResult();
    }

    private async Task SeedCategoriesAndEvents()
    {
        _music = new Category { Name = "Muzika", IconContentType = "image/png", IsActive = true };
        _sport = new Category { Name = "Sport", IconContentType = "image/png", IsActive = true };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.CategoryRepository.AddAsync(_sport);
        await _fixture.UnitOfWork.SaveChangesAsync();

        await _fixture.EventRepository.AddAsync(new Event
        {
            Id = Guid.NewGuid(),
            Name = "Ljetni Festival",
            OrganizationId = _orgA,
            CategoryId = _music.Id,
            Date = DateTime.UtcNow.AddDays(10),
            Status = PublishStatus.Draft,
        });
        await _fixture.EventRepository.AddAsync(new Event
        {
            Id = Guid.NewGuid(),
            Name = "Košarkaški Turnir",
            OrganizationId = _orgA,
            CategoryId = _sport.Id,
            Date = DateTime.UtcNow.AddDays(20),
            Status = PublishStatus.Published,
        });
        await _fixture.EventRepository.AddAsync(new Event
        {
            Id = Guid.NewGuid(),
            Name = "Jesenji Koncert",
            OrganizationId = _orgB,
            CategoryId = _music.Id,
            Date = DateTime.UtcNow.AddDays(30),
            Status = PublishStatus.Published,
        });
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_FiltersByOrganizationId()
    {
        var result = await _sut.GetAllAsync(new EventQuery { OrganizationId = _orgA });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(e => e.OrganizationId == _orgA);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByOrganizationAndCategory()
    {
        var result = await _sut.GetAllAsync(new EventQuery { OrganizationId = _orgA, CategoryId = _music.Id });

        result.Value!.Items.Should().ContainSingle(e => e.Name == "Ljetni Festival");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByFtsOnName()
    {
        var result = await _sut.GetAllAsync(new EventQuery { FTS = "Košarkaški" });

        result.Value!.Items.Should().ContainSingle(e => e.Name == "Košarkaški Turnir");
    }

    [Fact]
    public async Task GetAllAsync_MapsCategoryNameFromRelatedCategory()
    {
        var result = await _sut.GetAllAsync(new EventQuery { OrganizationId = _orgA, CategoryId = _sport.Id });

        result.Value!.Items.Single().CategoryName.Should().Be("Sport");
    }

    [Fact]
    public async Task GetAllAsync_WithNoFilters_ReturnsAllOrganizationsEvents()
    {
        var result = await _sut.GetAllAsync(new EventQuery());

        result.Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_ReturnsDistinctOrganizationIdsAcrossStatuses()
    {
        // Muzika events span both orgs (_orgA Draft, _orgB Published) — the org-list category
        // filter must include both statuses, not just Published, since this is an internal
        // admin-facing filter rather than the public catalog.
        var result = await _sut.GetOrganizationIdsAsync([_music.Id]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([_orgA, _orgB]);
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_ForCategoryWithNoEvents_ReturnsEmpty()
    {
        var unusedCategory = new Category { Name = "Tehnologija", IconContentType = "image/png", IsActive = true };
        await _fixture.CategoryRepository.AddAsync(unusedCategory);
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.GetOrganizationIdsAsync([unusedCategory.Id]);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_ForCategoryUsedByOnlyOneOrg_ReturnsOnlyThatOrg()
    {
        var result = await _sut.GetOrganizationIdsAsync([_sport.Id]);

        result.Value.Should().BeEquivalentTo([_orgA]);
    }

    public void Dispose() => _fixture.Dispose();
}
