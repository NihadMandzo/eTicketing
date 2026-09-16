using System.Security.Claims;
using System.Text;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;
using QuestPDF.Infrastructure;

namespace eTicketing.Ticketing.Business.Tests.Reports;

/// <summary>
/// The export path. These assert on the two things that can go wrong independently of the reports
/// themselves — who is allowed to export, and whether the renderer actually produces a PDF for
/// every tab — rather than re-testing the figures, which ReportServiceTests already covers.
/// </summary>
public class ReportPdfServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IReportPdfService _sut;

    private readonly Guid _org = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public ReportPdfServiceTests()
    {
        // The renderer needs the same QuestPDF licence the API sets at startup; without it
        // GeneratePdf throws rather than returning bytes.
        QuestPDF.Settings.License = LicenseType.Community;

        _sut = _fixture.CreateReportPdfService();

        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = _org,
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        _fixture.DbContext.Set<Sector>().Add(sector);
        _fixture.DbContext.SaveChanges();

        var ticket = Ticket.ForSingleOccurrence(
            sector.Id, null, Guid.NewGuid(), _productId, Guid.NewGuid(), "kupac@test.ba", 50m);
        _fixture.DbContext.Set<Ticket>().Add(ticket);
        _fixture.DbContext.SaveChanges();
        ticket.MarkValidated(Guid.NewGuid(), new DateTime(2026, 8, 12, 19, 30, 0, DateTimeKind.Utc));
        ticket.CreatedAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        _fixture.DbContext.SaveChanges();

        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CatalogProductResponse>
            {
                new(_productId, _org, PublishStatus.Published, TicketingMode.SingleOccurrence,
                    "Ljetni Festival", null, City.Mostar),
            });

        _fixture.CatalogClient
            .Setup(c => c.GetOrganizationProductStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogOrganizationProductStats(_org, 3, 2, 1, 1)]);

        _fixture.SeedOrganizationSnapshotAsync(_org, "Sunset Events", "Mostar").GetAwaiter().GetResult();
    }

    private static ClaimsPrincipal Caller(string role, Guid? organizationId = null)
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

    private static ReportExportQuery Query(ReportTab tab) => new()
    {
        Tab = tab,
        From = new DateOnly(2026, 8, 1),
        To = new DateOnly(2026, 8, 24),
    };

    [Theory]
    [InlineData(ReportTab.Sales)]
    [InlineData(ReportTab.Products)]
    [InlineData(ReportTab.Redemption)]
    [InlineData(ReportTab.Organizations)]
    [InlineData(ReportTab.Insights)]
    public async Task ExportAsync_ForSuperAdmin_RendersEveryTabAsAPdf(ReportTab tab)
    {
        var result = await _sut.ExportAsync(Query(tab), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().NotBeEmpty();
        // %PDF- is the file's magic number: proof the bytes are a real document rather than an
        // empty buffer that happened not to throw.
        Encoding.ASCII.GetString(result.Value.Content, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task ExportAsync_ForAdminExportingInsights_PropagatesTheTabsOwnRefusal()
    {
        // Insights sits with Sales on the matrix — it forecasts and segments money, which is not
        // Admin's remit — so the refusal is the tab's, not a separate export rule.
        var result = await _sut.ExportAsync(Query(ReportTab.Insights), Caller("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task ExportAsync_ForOrganizationAdminExportingInsights_ReturnsExportForbidden()
    {
        var result = await _sut.ExportAsync(Query(ReportTab.Insights), Caller("OrganizationAdmin", _org));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.export_forbidden");
    }

    [Fact]
    public async Task ExportAsync_ForInsights_NamesTheFileAfterThatTab()
    {
        var result = await _sut.ExportAsync(Query(ReportTab.Insights), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("izvjestaj-uvidi-2026-08-01-2026-08-24.pdf");
    }

    [Fact]
    public async Task ExportAsync_ForOrganizationAdmin_ReturnsForbiddenEvenForAnAllowedTab()
    {
        // Sales is a tab OrganizationAdmin can read on screen — exporting it is the separate
        // permission this service adds.
        var result = await _sut.ExportAsync(Query(ReportTab.Sales), Caller("OrganizationAdmin", _org));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.export_forbidden");
    }

    [Fact]
    public async Task ExportAsync_ForAdminExportingSales_PropagatesTheReportsOwnRefusal()
    {
        // Not a second, differently-worded refusal: the tab matrix already answered this.
        var result = await _sut.ExportAsync(Query(ReportTab.Sales), Caller("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task ExportAsync_ForOrganizationSuperAdmin_RendersItsOwnOrganizationsReport()
    {
        var result = await _sut.ExportAsync(Query(ReportTab.Sales), Caller("OrganizationSuperAdmin", _org));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportAsync_NamesTheFileAfterTheTabAndTheRange()
    {
        var result = await _sut.ExportAsync(Query(ReportTab.Products), Caller("SuperAdmin"));

        result.Value!.FileName.Should().Be("izvjestaj-proizvodi-2026-08-01-2026-08-24.pdf");
    }

    [Fact]
    public async Task ExportAsync_ForAPeriodWithNoData_StillRendersADocument()
    {
        var empty = new ReportExportQuery
        {
            Tab = ReportTab.Sales,
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 1, 31),
        };

        var result = await _sut.ExportAsync(empty, Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().NotBeEmpty();
    }

    public void Dispose() => _fixture.Dispose();
}
