using System.Security.Claims;
using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Recommendations;

/// <summary>
/// Covers the fallback chain (Personalized → ContentBased → Popular) and the exclusions every path
/// shares. The model itself is faked (see <see cref="FakeRecommendationModel"/>) so a test can
/// state what the model knows instead of training a real factorization on a handful of rows and
/// hoping it agrees; MatrixFactorizationModelTests exercises the real one.
/// </summary>
public class RecommendationServiceTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IRecommendationService _sut;

    private readonly Guid _buyer = Guid.NewGuid();
    private readonly Guid _otherBuyer = Guid.NewGuid();
    private readonly Guid _org = Guid.NewGuid();

    private Category _music = null!;
    private Category _sport = null!;
    private Category _museum = null!;

    private Product _concertMostar = null!;
    private Product _concertSarajevo = null!;
    private Product _matchMostar = null!;
    private Product _museumMostar = null!;
    private Product _draftConcert = null!;
    private Product _pastConcert = null!;

    public RecommendationServiceTests()
    {
        _sut = _fixture.CreateRecommendationService();
        SeedAsync().GetAwaiter().GetResult();
    }

    // ─── Tracking views ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrackViewAsync_ForTheSameProductTwice_IncrementsCountInsteadOfAddingARow()
    {
        await _sut.TrackViewAsync(new TrackViewRequest(_concertMostar.Id), Buyer());
        await _sut.TrackViewAsync(new TrackViewRequest(_concertMostar.Id), Buyer());

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);

        history.Should().ContainSingle()
            .Which.Count.Should().Be(2, "repeat views bump the counter — the table is one row per (user, product, type)");
    }

    [Fact]
    public async Task TrackViewAsync_WhenAConcurrentWriterInsertedTheSameRowFirst_IncrementsInsteadOfFailing()
    {
        // Two tabs on the same product: both read no row, both try to insert, and the unique index
        // rejects whoever commits second. The loser must land as an increment, not as a 500.
        var racing = new RacingUnitOfWork(
            _fixture.UnitOfWork,
            () => InsertInteractionThroughAnotherWriterAsync(_buyer, _concertMostar.Id, InteractionType.View));

        var result = await _fixture.CreateRecommendationService(racing)
            .TrackViewAsync(new TrackViewRequest(_concertMostar.Id), Buyer());

        result.IsSuccess.Should().BeTrue();
        racing.SaveAttempts.Should().Be(2, "the first commit lost the race and the recovery re-ran the upsert");

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().ContainSingle()
            .Which.Count.Should().Be(2, "the winner's row plus this view — both views actually happened");
    }

    [Fact]
    public async Task TrackViewAsync_ForAnUnknownProduct_ReturnsNotFound()
    {
        var result = await _sut.TrackViewAsync(new TrackViewRequest(Guid.NewGuid()), Buyer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.not_found");
    }

    [Fact]
    public async Task TrackViewAsync_ForADraftProduct_ReturnsNotFound()
    {
        var result = await _sut.TrackViewAsync(new TrackViewRequest(_draftConcert.Id), Buyer());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.not_found");

        var history = await _fixture.UserInteractionRepository.GetByUserAsync(_buyer);
        history.Should().BeEmpty("a draft must not be probeable by watching which ids this endpoint accepts");
    }

    // ─── Choosing a strategy ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForMeAsync_ForAUserWithNoHistory_FallsBackToPopular()
    {
        var result = await _sut.GetForMeAsync(new RecommendationQuery(), Buyer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Source.Should().Be(RecommendationSource.Popular);
        result.Value.Items.Should().NotBeEmpty("an empty row is worse than a non-personalized one");
    }

    [Fact]
    public async Task GetForMeAsync_ForAUserWithOneView_ReturnsContentBasedFromTheSameCategory()
    {
        await TrackAsync(_buyer, _concertSarajevo, InteractionType.View);

        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 1 }, Buyer());

        result.Value!.Source.Should().Be(RecommendationSource.ContentBased);
        result.Value.Items.Should().ContainSingle()
            .Which.CategoryId.Should().Be(_music.Id, "the only thing known about this user is that they like music");
    }

    [Fact]
    public async Task GetForMeAsync_WithEnoughHistoryAndATrainedModel_ReturnsPersonalized()
    {
        await GiveBuyerEnoughHistoryAsync();
        _fixture.RecommendationModel.SetScore(_buyer, _matchMostar.Id, 0.9f);

        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 3 }, Buyer());

        result.Value!.Source.Should().Be(RecommendationSource.Personalized);
        result.Value.Items[0].Id.Should().Be(_matchMostar.Id, "the model scored it highest");
    }

    [Fact]
    public async Task GetForMeAsync_WithEnoughHistoryButAModelThatHasNeverSeenTheUser_ReportsContentBased()
    {
        await GiveBuyerEnoughHistoryAsync();
        // Trained, but only ever on somebody else — the exact state of a user who signed up after
        // the last nightly run. Claiming "Personalized" here would be a lie.
        _fixture.RecommendationModel.SetScore(_otherBuyer, _matchMostar.Id, 0.9f);

        var result = await _sut.GetForMeAsync(new RecommendationQuery(), Buyer());

        result.Value!.Source.Should().Be(RecommendationSource.ContentBased);
    }

    // ─── Exclusions every path shares ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForMeAsync_NeverReturnsAProductTheUserAlreadyBought()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);

        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 24 }, Buyer());

        result.Value!.Items.Should().NotContain(p => p.Id == _concertMostar.Id);
    }

    [Fact]
    public async Task GetForMeAsync_NeverReturnsADraftProduct()
    {
        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 24 }, Buyer());

        result.Value!.Items.Should().NotContain(p => p.Id == _draftConcert.Id);
    }

    [Fact]
    public async Task GetForMeAsync_ForAPastSingleOccurrenceProduct_ExcludesIt()
    {
        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 24 }, Buyer());

        result.Value!.Items.Should().NotContain(p => p.Id == _pastConcert.Id,
            "a concert that already happened cannot be bought");
    }

    [Fact]
    public async Task GetForMeAsync_RespectsTheTakeLimit()
    {
        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 2 }, Buyer());

        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetForMeAsync_WhenTheModelRecognisesOnlyOneProduct_PadsTheRowFromTheFallbacks()
    {
        await GiveBuyerEnoughHistoryAsync();
        _fixture.RecommendationModel.SetScore(_buyer, _matchMostar.Id, 0.9f);

        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 3 }, Buyer());

        result.Value!.Source.Should().Be(RecommendationSource.Personalized);
        result.Value.Items.Should().HaveCount(3, "a row of one card looks broken");
        result.Value.Items.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    // ─── Similar products ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSimilarAsync_ForAProductWithCoPurchasers_RanksTheCoPurchasedProductFirst()
    {
        // Two different people bought the concert and then the match. Nothing about the two
        // products resembles the other — different category, different recency — so only
        // co-purchase evidence can put it first.
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);
        await TrackAsync(_buyer, _matchMostar, InteractionType.Purchase);
        await TrackAsync(_otherBuyer, _concertMostar, InteractionType.Purchase);
        await TrackAsync(_otherBuyer, _matchMostar, InteractionType.Purchase);

        var result = await _sut.GetSimilarAsync(_concertMostar.Id, new SimilarProductsQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value![0].Id.Should().Be(_matchMostar.Id);
    }

    [Fact]
    public async Task GetSimilarAsync_ForAProductWithNoInteractions_StillReturnsSameCategoryProducts()
    {
        var result = await _sut.GetSimilarAsync(_concertMostar.Id, new SimilarProductsQuery { Take = 1 });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.CategoryId.Should().Be(_music.Id, "with no collaborative signal it falls back to resemblance");
    }

    [Fact]
    public async Task GetSimilarAsync_NeverIncludesTheProductItself()
    {
        var result = await _sut.GetSimilarAsync(_concertMostar.Id, new SimilarProductsQuery { Take = 24 });

        result.Value!.Should().NotContain(p => p.Id == _concertMostar.Id);
    }

    [Fact]
    public async Task GetSimilarAsync_ForADraftProduct_ReturnsNotFound()
    {
        var result = await _sut.GetSimilarAsync(_draftConcert.Id, new SimilarProductsQuery());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.not_found");
    }

    // ─── Popularity ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPopularAsync_FilteredByCity_ReturnsOnlyThatCitysProducts()
    {
        await TrackAsync(_buyer, _concertSarajevo, InteractionType.Purchase);

        var result = await _sut.GetPopularAsync(new PopularProductsQuery { City = City.Mostar, Take = 24 });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().NotBeEmpty();
        result.Value.Should().OnlyContain(p => p.City == City.Mostar);
    }

    [Fact]
    public async Task GetPopularAsync_RanksTheMostBoughtProductFirst()
    {
        await TrackAsync(_buyer, _matchMostar, InteractionType.Purchase);
        await TrackAsync(_otherBuyer, _matchMostar, InteractionType.Purchase);
        await TrackAsync(_buyer, _museumMostar, InteractionType.Purchase);

        var result = await _sut.GetPopularAsync(new PopularProductsQuery { Take = 24 });

        result.Value![0].Id.Should().Be(_matchMostar.Id, "two buyers beat one");
    }

    [Fact]
    public async Task GetPopularAsync_ForABestSellerOlderThanTheCandidateCap_StillRanksItFirst()
    {
        // MaxCandidates bounds in-memory scoring, and the pool it keeps is the NEWEST products —
        // so a best seller old enough to fall out of it used to vanish from the popularity list
        // entirely. Popularity is ranked in SQL across the whole catalog precisely so it can't.
        _fixture.RecommendationOptions.MaxCandidates = 2;
        await BackdateAsync(_concertMostar, TimeSpan.FromDays(30));
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);
        await TrackAsync(_otherBuyer, _concertMostar, InteractionType.Purchase);

        var result = await _sut.GetPopularAsync(new PopularProductsQuery { Take = 24 });

        result.IsSuccess.Should().BeTrue();
        result.Value![0].Id.Should().Be(
            _concertMostar.Id, "the most-bought product in the catalog heads the popularity list whatever its age");
    }

    [Fact]
    public async Task GetForMeAsync_ForAUserWithNoHistory_SurfacesABestSellerOlderThanTheCandidateCap()
    {
        _fixture.RecommendationOptions.MaxCandidates = 2;
        await BackdateAsync(_concertMostar, TimeSpan.FromDays(30));

        // Bought by somebody else: a product the caller already owns is excluded from their own
        // recommendations, and this test is about the caller having no history at all.
        await TrackAsync(_otherBuyer, _concertMostar, InteractionType.Purchase);

        var result = await _sut.GetForMeAsync(new RecommendationQuery { Take = 6 }, Buyer());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Source.Should().Be(RecommendationSource.Popular);
        result.Value.Items[0].Id.Should().Be(_concertMostar.Id);
    }

    [Fact]
    public async Task GetPopularAsync_WithNoPurchasesAtAll_StillReturnsPublishedProducts()
    {
        var result = await _sut.GetPopularAsync(new PopularProductsQuery { Take = 24 });

        result.Value!.Should().NotBeEmpty("this is the state of a freshly deployed platform");
        result.Value.Should().NotContain(p => p.Id == _draftConcert.Id);
    }

    // ─── Model lifecycle ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetrainAsync_WithNoInteractions_ReturnsConflictInsteadOfTrainingAnEmptyModel()
    {
        var result = await _sut.RetrainAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("recommendation.no_training_data");
        _fixture.RecommendationModel.TrainingRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrainAsync_WithInteractions_TrainsAndRecordsAnActiveSnapshot()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);

        var result = await _sut.RetrainAsync();

        result.IsSuccess.Should().BeTrue();
        _fixture.RecommendationModel.TrainingRuns.Should().ContainSingle();

        var active = await _fixture.ModelSnapshotRepository.GetActiveAsync();
        active.Should().NotBeNull();
        active!.InteractionCount.Should().Be(1);
        result.Value!.TrainedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrainAsync_RunTwice_LeavesExactlyOneActiveSnapshot()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);

        await _sut.RetrainAsync();
        await _sut.RetrainAsync();

        var history = await _fixture.ModelSnapshotRepository.GetRecentAsync(10);
        history.Should().HaveCount(2);
        history.Count(s => s.IsActive).Should().Be(1);
    }

    [Fact]
    public async Task EnsureModelLoadedAsync_WithAnExistingSnapshot_LoadsItInsteadOfRetraining()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);
        await _sut.RetrainAsync();

        // A fresh service over the same database, with a model that has not been loaded yet —
        // exactly what a restarted container looks like.
        var restarted = _fixture.CreateRecommendationService();
        _fixture.RecommendationModel.IsTrained = false;
        _fixture.RecommendationModel.TrainingRuns.Clear();

        await restarted.EnsureModelLoadedAsync();

        _fixture.RecommendationModel.LoadedBlobNames.Should().ContainSingle();
        _fixture.RecommendationModel.TrainingRuns.Should().BeEmpty("a restart must not throw away what was learned");
    }

    [Fact]
    public async Task EnsureModelLoadedAsync_WhenTheStoredModelIsGone_RetrainsInstead()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);
        await _sut.RetrainAsync();

        _fixture.RecommendationModel.IsTrained = false;
        _fixture.RecommendationModel.LoadSucceeds = false;
        _fixture.RecommendationModel.TrainingRuns.Clear();

        await _fixture.CreateRecommendationService().EnsureModelLoadedAsync();

        _fixture.RecommendationModel.TrainingRuns.Should().ContainSingle(
            "a snapshot row that outlived its blob is recoverable, not fatal");
    }

    [Fact]
    public async Task GetStatusAsync_ReportsInteractionTotalsAndTrainingHistory()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.Purchase);
        await TrackAsync(_buyer, _matchMostar, InteractionType.View);
        await _sut.RetrainAsync();

        var result = await _sut.GetStatusAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalInteractions.Should().Be(2);
        result.Value.PurchaseCount.Should().Be(1);
        result.Value.ViewCount.Should().Be(1);
        result.Value.DistinctUsers.Should().Be(1);
        result.Value.History.Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    // ─── Fixtures ────────────────────────────────────────────────────────────────────────────

    private ClaimsPrincipal Buyer() => BuildCaller(_buyer);

    private static ClaimsPrincipal BuildCaller(Guid userId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "User")],
            "TestAuth"));

    /// <summary>Three distinct interactions — the default MinInteractionsForPersonalized — so the
    /// service is willing to consult the model at all.</summary>
    private async Task GiveBuyerEnoughHistoryAsync()
    {
        await TrackAsync(_buyer, _concertMostar, InteractionType.View);
        await TrackAsync(_buyer, _concertSarajevo, InteractionType.View);
        await TrackAsync(_buyer, _museumMostar, InteractionType.View);
    }

    private async Task TrackAsync(Guid userId, Product product, InteractionType type)
    {
        await _fixture.UserInteractionRepository.UpsertAsync(userId, product.Id, type, DateTime.UtcNow);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    /// <summary>Commits an interaction through a genuinely separate DbContext — the competing
    /// writer in the upsert-race tests. The audit interceptor only stamps CreatedAt on insert, so
    /// this row looks exactly like one written by another request.</summary>
    private async Task InsertInteractionThroughAnotherWriterAsync(Guid userId, Guid productId, InteractionType type)
    {
        await using var other = _fixture.NewDbContext();

        other.Add(new UserInteraction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            Type = type,
            Count = 1,
            LastOccurredAt = DateTime.UtcNow,
        });

        await other.SaveChangesAsync();
    }

    /// <summary>Pushes a product out of the newest-MaxCandidates window. CreatedAt is set by the
    /// audit interceptor on insert only, so re-saving it as Modified is how a test gets a product
    /// with a genuinely older creation date.</summary>
    private async Task BackdateAsync(Product product, TimeSpan age)
    {
        product.CreatedAt = DateTime.UtcNow - age;
        _fixture.ProductRepository.Update(product);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    private async Task SeedAsync()
    {
        _music = new Category { Name = "Muzika", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _sport = new Category { Name = "Sport", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _museum = new Category { Name = "Muzej", IsActive = true, TicketingMode = TicketingMode.DailyEntry };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.CategoryRepository.AddAsync(_sport);
        await _fixture.CategoryRepository.AddAsync(_museum);
        await _fixture.UnitOfWork.SaveChangesAsync();

        _concertMostar = await AddProductAsync("Koncert Mostar", _music, City.Mostar, DateTime.UtcNow.AddDays(10));
        _concertSarajevo = await AddProductAsync("Koncert Sarajevo", _music, City.Sarajevo, DateTime.UtcNow.AddDays(12));
        _matchMostar = await AddProductAsync("Utakmica Mostar", _sport, City.Mostar, DateTime.UtcNow.AddDays(14));
        _museumMostar = await AddProductAsync("Muzej Mostar", _museum, City.Mostar, date: null);
        _draftConcert = await AddProductAsync("Nacrt koncerta", _music, City.Mostar, DateTime.UtcNow.AddDays(9), PublishStatus.Draft);
        _pastConcert = await AddProductAsync("Prošli koncert", _music, City.Mostar, DateTime.UtcNow.AddDays(-3));
    }

    private async Task<Product> AddProductAsync(
        string name, Category category, City city, DateTime? date, PublishStatus status = PublishStatus.Published)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = name,
            OrganizationId = _org,
            CategoryId = category.Id,
            City = city,
            Date = date,
            Status = status,
            Latitude = 43.34,
            Longitude = 17.81,
        };

        await _fixture.ProductRepository.AddAsync(product);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return product;
    }

    public void Dispose() => _fixture.Dispose();
}
