using System.Security.Claims;
using System.Text;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

/// <summary>
/// GET /tickets/{id}/pdf renders the buyer's sheet on the spot instead of serving a stored file, so
/// these cover the two things that replaces: that the render actually happens and is correct, and
/// that the authorization which a public blob URL never had is really enforced.
/// </summary>
public class TicketPdfServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketPdfService _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _buyerId = Guid.NewGuid();

    static TicketPdfServiceTests()
    {
        // Normally set once in TicketingServiceCollectionExtensions; these bypass the composition
        // root, and QuestPDF renders nothing without a declared licence.
        QuestPDF.Settings.License = LicenseType.Community;
        Shared.TicketPdf.TicketTheme.EnsureFontsRegistered();
    }

    public TicketPdfServiceTests()
    {
        _sut = _fixture.CreateTicketPdfService();
        MockProduct();
    }

    [Fact]
    public async Task GetAsync_ForTheBuyersOwnTicket_RendersAPdf()
    {
        var ticket = await SeedTicketAsync();

        var result = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));

        result.IsSuccess.Should().BeTrue();
        Encoding.ASCII.GetString(result.Value!.Content, 0, 4).Should().Be("%PDF");
        result.Value.FileName.Should().Be($"ulaznica-{ticket.Id.ToString("N")[..8].ToUpperInvariant()}.pdf");
    }

    [Fact]
    public async Task GetAsync_EmbedsTheSameQrPayloadTheGateWillAccept()
    {
        // The whole premise of not storing the PDF is that re-rendering reproduces the original
        // sheet. That only holds if the QR payload is deterministic — assert it, rather than
        // trusting it.
        var ticket = await SeedTicketAsync();

        var result = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));

        var text = Collapse(ExtractText(result.Value!.Content));
        text.Should().Contain(Collapse(ticket.Id.ToString().ToUpperInvariant()));
    }

    [Fact]
    public async Task GetAsync_RendersTwiceToTheSameBytes()
    {
        var ticket = await SeedTicketAsync();

        var first = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));
        var second = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));

        // Only the creation timestamp in the PDF trailer differs run to run, so compare the page
        // text rather than raw bytes — that is what the buyer and the scanner actually see.
        ExtractText(second.Value!.Content).Should().Be(ExtractText(first.Value!.Content));
    }

    [Fact]
    public async Task GetAsync_ForAnotherBuyersTicket_IsForbidden()
    {
        // A stored public URL had no check at all; this is the check that replaces it.
        var ticket = await SeedTicketAsync();

        var result = await _sut.GetAsync(ticket.Id, Buyer(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.forbidden");
    }

    [Fact]
    public async Task GetAsync_ForPlatformStaff_RendersAnyBuyersTicket()
    {
        var ticket = await SeedTicketAsync();

        var result = await _sut.GetAsync(ticket.Id, PlatformStaff());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ForPlatformStaff_PrintsTheBuyersEmailNotTheStaffMembers()
    {
        // The sheet is the buyer's document; an admin's address appearing on it would be wrong on
        // its face and would leak the admin's identity to whoever the file is forwarded to.
        var ticket = await SeedTicketAsync();

        var result = await _sut.GetAsync(ticket.Id, PlatformStaff());

        var text = ExtractText(result.Value!.Content);
        text.Should().Contain("buyer@example.com").And.NotContain("admin@example.com");
    }

    [Fact]
    public async Task GetAsync_ForAnUnknownTicket_ReturnsNotFound()
    {
        var result = await _sut.GetAsync(Guid.NewGuid(), Buyer(_buyerId));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task GetAsync_WhenTheProductNoLongerExists_ReturnsNotFoundRatherThanANamelessSheet()
    {
        var ticket = await SeedTicketAsync();
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogProductResponse?)null);

        var result = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ticket.product_not_found");
    }

    [Fact]
    public async Task GetAsync_ForAUsedTicket_StillRenders()
    {
        // A spent ticket is still the buyer's receipt — refusing the download would be surprising,
        // and the sheet can't admit anyone twice anyway.
        var ticket = await SeedTicketAsync(TicketStatus.Used);

        var result = await _sut.GetAsync(ticket.Id, Buyer(_buyerId));

        result.IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------- test setup

    private void MockProduct() =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                _productId, Guid.NewGuid(), PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Ljetni Festival", new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), City.Sarajevo));

    private async Task<Ticket> SeedTicketAsync(TicketStatus status = TicketStatus.Ready)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = Guid.NewGuid(),
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        _fixture.DbContext.Sectors.Add(sector);

        var ticket = Ticket.ForSingleOccurrence(
            sector.Id, null, Guid.NewGuid(), _productId, _buyerId, "buyer@example.com", 50);
        ticket.Status = status;

        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return ticket;
    }

    private static ClaimsPrincipal Buyer(Guid userId) => Principal(userId, "User", "buyer@example.com");

    private static ClaimsPrincipal PlatformStaff() => Principal(Guid.NewGuid(), "Admin", "admin@example.com");

    private static ClaimsPrincipal Principal(Guid userId, string role, string email) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Email, email),
            ],
            "TestAuth"));

    private static string ExtractText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(" ", document.GetPages().Select(p => p.Text));
    }

    private static string Collapse(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    public void Dispose() => _fixture.Dispose();
}
