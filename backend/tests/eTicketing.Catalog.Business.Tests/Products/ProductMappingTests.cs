using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Mapster;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// The three projections this service moved onto Mapster, plus the config compile.
///
/// <para>The one that actually needs watching is TicketingMode on the internal response. It is read
/// off <c>Product.Category</c>, so a caller that forgot the Include gets Mapster's silent default —
/// <see cref="TicketingMode.SingleOccurrence"/>, ordinal zero, which is precisely the mode every
/// DailyEntry and RecurringReservation branch in eTicketing.Ticketing is not. A wrong value there
/// does not throw anywhere; it makes a museum day-pass validate like a one-night concert.</para>
/// </summary>
public class ProductMappingTests
{
    [Fact]
    public void GlobalConfig_Compiles()
    {
        // Catches a config naming a member that has since been renamed or removed — the one Mapster
        // mistake that is otherwise invisible until a response comes back half empty.
        var compile = () => TypeAdapterConfig.GlobalSettings.Compile();

        compile.Should().NotThrow();
    }

    [Fact]
    public void Product_MapsToTheInternalResponseIncludingTheCategorysTicketingMode()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Muzej — dnevna ulaznica",
            Date = null,
            City = City.Mostar,
            Status = PublishStatus.Published,
            CategoryId = 7,
            Category = new Category { Id = 7, Name = "Muzeji", TicketingMode = TicketingMode.DailyEntry },
        };

        var response = product.Adapt<ProductInternalResponse>();

        response.Id.Should().Be(product.Id);
        response.OrganizationId.Should().Be(product.OrganizationId);
        response.Name.Should().Be("Muzej — dnevna ulaznica");
        response.City.Should().Be(City.Mostar);
        response.Status.Should().Be(PublishStatus.Published);
        // The whole reason this mapping needs a config rather than convention.
        response.TicketingMode.Should().Be(TicketingMode.DailyEntry);
    }

    [Fact]
    public void Product_MapsADateItHas()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Ljetni Festival",
            Date = new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc),
            City = City.Sarajevo,
            Status = PublishStatus.Published,
            Category = new Category { Id = 1, Name = "Koncerti", TicketingMode = TicketingMode.SingleOccurrence },
        };

        product.Adapt<ProductInternalResponse>().Date
            .Should().Be(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void OrganizationStats_MapEveryCountRatherThanTransposingThem()
    {
        // Four same-typed neighbours. A transposition here shows platform staff an organization with
        // five published products and four drafts as the reverse, and nothing about it looks wrong.
        var stats = new OrganizationProductStats(Guid.NewGuid(), Total: 9, Published: 5, Draft: 4, WithoutImage: 2);

        var response = stats.Adapt<OrganizationProductStatsResponse>();

        response.OrganizationId.Should().Be(stats.OrganizationId);
        response.Total.Should().Be(9);
        response.Published.Should().Be(5);
        response.Draft.Should().Be(4);
        response.WithoutImage.Should().Be(2);
    }

    [Fact]
    public void ModelSnapshot_MapsToTheBackOfficeShapeWithoutTheBlobName()
    {
        var snapshot = new RecommendationModelSnapshot
        {
            Id = Guid.NewGuid(),
            BlobName = "model-2026-08-24.zip",
            TrainedAt = new DateTime(2026, 8, 24, 3, 0, 0, DateTimeKind.Utc),
            InteractionCount = 1200,
            UserCount = 300,
            ProductCount = 80,
            TrainingDurationMs = 4500,
            IsActive = true,
        };

        var response = snapshot.Adapt<ModelSnapshotResponse>();

        response.Id.Should().Be(snapshot.Id);
        response.TrainedAt.Should().Be(snapshot.TrainedAt);
        response.InteractionCount.Should().Be(1200);
        response.UserCount.Should().Be(300);
        response.ProductCount.Should().Be(80);
        response.TrainingDurationMs.Should().Be(4500);
        response.IsActive.Should().BeTrue();
        // BlobName is a private-container key and has no member on the response to land in —
        // Mapster drops unmatched source members, which here is the behaviour we want.
        typeof(ModelSnapshotResponse).GetProperty("BlobName").Should().BeNull();
    }
}
