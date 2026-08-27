using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace eTicketing.Ticketing.Business.Tests.TicketPrint;

/// <summary>
/// The renderer is what turns a queued batch into paper, and the two things that must never drift
/// are the sheet arithmetic (exactly three tickets per A4, so a guillotine operator can cut a stack
/// blind) and the failure handling (a batch must always end up Ready or Failed, never stuck on a
/// spinner the organizer waits on forever).
/// </summary>
public class TicketPrintRendererTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketPrintRenderer _sut;

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    static TicketPrintRendererTests()
    {
        // Normally set once in TicketingServiceCollectionExtensions; these bypass the composition
        // root, and QuestPDF renders nothing without a declared licence.
        QuestPDF.Settings.License = LicenseType.Community;
        Shared.TicketPdf.TicketTheme.EnsureFontsRegistered();
    }

    public TicketPrintRendererTests()
    {
        _sut = _fixture.CreateTicketPrintRenderer();
        MockProduct(TicketingMode.SingleOccurrence);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(50, 17)]
    public async Task RenderAsync_PutsExactlyThreeTicketsOnEachA4Sheet(int ticketCount, int expectedPages)
    {
        var batch = await SeedBatchAsync(ticketCount);

        await _sut.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.Status.Should().Be(TicketPrintBatchStatus.Ready);
        stored.PageCount.Should().Be(expectedPages);

        using var pdf = PdfDocument.Open(await FileOfAsync(batch.Id));
        pdf.NumberOfPages.Should().Be(expectedPages);
    }

    [Fact]
    public async Task RenderAsync_RecordsTheFileSizeAndProgressItFinishedWith()
    {
        var batch = await SeedBatchAsync(5);

        await _sut.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.RenderedCount.Should().Be(5);
        stored.FileSizeBytes.Should().BeGreaterThan(0);
        stored.CompletedAt.Should().NotBeNull();
        stored.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_RunsGeneratePdfOffTheCallingThread()
    {
        // QuestPDF's GeneratePdf() is a long, pure-CPU call for a large batch — running it inline
        // on whatever thread-pool worker resumed this async method would tie that worker up for
        // the whole render, starving every other request the host is trying to serve concurrently
        // (this is exactly what surfaced as Gateway "RequestTimedOut" errors on unrelated
        // endpoints while a big export was rendering). Dispatching it via
        // TaskCreationOptions.LongRunning is what fixes that, so the guarantee worth pinning down
        // is that the render provably does not happen on the thread that called RenderAsync.
        var batch = await SeedBatchAsync(3);
        var callingThreadId = Environment.CurrentManagedThreadId;
        var renderer = (TicketPrintRenderer)_sut;

        await renderer.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.Status.Should().Be(TicketPrintBatchStatus.Ready);
        renderer.LastGeneratePdfThreadId.Should().NotBeNull();
        renderer.LastGeneratePdfThreadId.Should().NotBe(callingThreadId);
    }

    [Fact]
    public async Task RenderAsync_PrintsEveryTicketsOwnGateCodeAndStubNumber()
    {
        // Each sheet is torn apart and sold separately, so two tickets sharing a code would let two
        // people through on one admission — assert the codes are actually distinct and present.
        var batch = await SeedBatchAsync(3);
        var tickets = await _fixture.DbContext.Tickets.AsNoTracking().OrderBy(t => t.SerialNumber).ToListAsync();

        await _sut.RenderAsync(batch.Id);

        var text = Collapse(await ExtractTextAsync(batch.Id));
        foreach (var ticket in tickets)
        {
            text.Should().Contain(Collapse(ticket.Id.ToString().ToUpperInvariant()));
            text.Should().Contain($"#{ticket.SerialNumber:D6}");
        }
    }

    [Fact]
    public async Task RenderAsync_PrintsTheBosnianLabelsWithTheirDiacritics()
    {
        // A wrong font subset shows up as blank boxes rather than an exception, so the diacritics
        // are worth asserting on directly.
        var batch = await SeedBatchAsync(1);

        await _sut.RenderAsync(batch.Id);

        var text = await ExtractTextAsync(batch.Id);
        text.Should().Contain("DOGAĐAJ");
        text.Should().Contain("SERIJSKI BROJ");
        text.Should().Contain("LOKACIJA");
        text.Should().Contain("SEKTOR");
        text.Should().Contain("CIJENA");
        text.Should().Contain("VRIJEDI");
    }

    [Fact]
    public async Task RenderAsync_PrintsPricesInBosnianNotation()
    {
        var batch = await SeedBatchAsync(1);

        await _sut.RenderAsync(batch.Id);

        Collapse(await ExtractTextAsync(batch.Id)).Should().Contain("80,00KM");
    }

    [Fact]
    public async Task RenderAsync_WithAMaximumLengthProductName_StillFitsThreeToASheet()
    {
        // Product names are validated up to 200 characters. Unclamped, one would burst its slot and
        // push the sheet count up — quietly wasting two thirds of every page.
        MockProduct(TicketingMode.SingleOccurrence, name: new string('A', 200));
        var batch = await SeedBatchAsync(3);

        await _sut.RenderAsync(batch.Id);

        (await ReloadAsync(batch.Id)).PageCount.Should().Be(1);
        using var pdf = PdfDocument.Open(await FileOfAsync(batch.Id));
        pdf.NumberOfPages.Should().Be(1);
    }

    [Fact]
    public async Task RenderAsync_ForADailyEntryBatch_PrintsTheDayEveryTicketAdmits()
    {
        MockProduct(TicketingMode.DailyEntry);
        var batch = await SeedBatchAsync(2, validDate: new DateOnly(2026, 8, 30));

        await _sut.RenderAsync(batch.Id);

        var text = await ExtractTextAsync(batch.Id);
        text.Should().Contain("30.08.2026");
        text.Should().Contain("DNEVNI ULAZ");
    }

    [Fact]
    public async Task RenderAsync_WhenTheProductNoLongerExists_FailsTheBatchWithAReason()
    {
        var batch = await SeedBatchAsync(3);
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogProductResponse?)null);

        await _sut.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.Status.Should().Be(TicketPrintBatchStatus.Failed);
        stored.ErrorMessage.Should().Contain("više ne postoji");
    }

    [Fact]
    public async Task RenderAsync_ForABatchWithNoTickets_FailsRatherThanProducingABlankSheet()
    {
        var batch = await SeedBatchAsync(0);

        await _sut.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.Status.Should().Be(TicketPrintBatchStatus.Failed);
    }

    [Fact]
    public async Task RenderAsync_ForAnAlreadyReadyBatch_LeavesItAlone()
    {
        // Startup recovery can race a fresh enqueue. Re-rendering would hand the organizer a second,
        // different file for the same batch.
        var batch = await SeedBatchAsync(3);
        await _sut.RenderAsync(batch.Id);
        var firstSize = (await ReloadAsync(batch.Id)).FileSizeBytes;

        await _sut.RenderAsync(batch.Id);

        var stored = await ReloadAsync(batch.Id);
        stored.FileSizeBytes.Should().Be(firstSize);
        (await _fixture.DbContext.Set<TicketPrintBatchFile>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RenderAsync_ForAnUnknownBatch_DoesNothingRatherThanThrowing()
    {
        // The worker must survive a batch that vanished between the enqueue and its turn.
        var act = async () => await _sut.RenderAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecoverUnfinishedAsync_PutsAStrandedRenderBackInTheQueue()
    {
        var batch = await SeedBatchAsync(3, status: TicketPrintBatchStatus.Rendering);

        var recovered = await _sut.RecoverUnfinishedAsync();

        recovered.Should().Contain(batch.Id);
        (await ReloadAsync(batch.Id)).Status.Should().Be(TicketPrintBatchStatus.Queued);
    }

    [Fact]
    public async Task RecoverUnfinishedAsync_LeavesFinishedBatchesAlone()
    {
        var ready = await SeedBatchAsync(3, status: TicketPrintBatchStatus.Ready);
        var failed = await SeedBatchAsync(3, status: TicketPrintBatchStatus.Failed);

        var recovered = await _sut.RecoverUnfinishedAsync();

        recovered.Should().NotContain(ready.Id).And.NotContain(failed.Id);
    }

    [Fact]
    public async Task SweepStaleFilesAsync_DestroysUncollectedSheetsButKeepsTheBatchRecord()
    {
        var batch = await SeedBatchAsync(3);
        await _sut.RenderAsync(batch.Id);
        _fixture.Clock.Advance(TimeSpan.FromDays(8));

        var swept = await _sut.SweepStaleFilesAsync(TimeSpan.FromDays(7));

        swept.Should().Be(1);
        (await _fixture.DbContext.Set<TicketPrintBatchFile>().CountAsync()).Should().Be(0);
        // The tickets stay valid and the batch stays on the record — only the paper is gone.
        (await ReloadAsync(batch.Id)).Should().NotBeNull();
        (await _fixture.DbContext.Tickets.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task SweepStaleFilesAsync_KeepsSheetsThatAreStillWithinRetention()
    {
        var batch = await SeedBatchAsync(3);
        await _sut.RenderAsync(batch.Id);
        _fixture.Clock.Advance(TimeSpan.FromDays(2));

        var swept = await _sut.SweepStaleFilesAsync(TimeSpan.FromDays(7));

        swept.Should().Be(0);
        (await _fixture.DbContext.Set<TicketPrintBatchFile>().CountAsync()).Should().Be(1);
    }

    // ------------------------------------------------------------------------------- test setup

    private void MockProduct(TicketingMode mode, string name = "Ljetni Festival") =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                _productId, _organizationId, PublishStatus.Published, mode,
                name, new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), City.Sarajevo));

    private async Task<TicketPrintBatch> SeedBatchAsync(
        int ticketCount,
        TicketPrintBatchStatus status = TicketPrintBatchStatus.Queued,
        DateOnly? validDate = null)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = _organizationId,
            Name = "VIP",
            Capacity = 500,
            Price = 80,
            Status = PublishStatus.Published,
            TicketingMode = validDate is null ? TicketingMode.SingleOccurrence : TicketingMode.DailyEntry,
        };
        _fixture.DbContext.Sectors.Add(sector);

        var batchId = Guid.NewGuid();
        var batch = new TicketPrintBatch
        {
            Id = batchId,
            ProductId = _productId,
            OrganizationId = _organizationId,
            RequestedByUserId = Guid.NewGuid(),
            ProductName = "Ljetni Festival",
            Status = status,
            TicketCount = ticketCount,
            SerialFrom = ticketCount == 0 ? 0 : 1,
            SerialTo = ticketCount,
            NominalValue = 80 * ticketCount,
            ValidDate = validDate,
            CompletedAt = status is TicketPrintBatchStatus.Ready or TicketPrintBatchStatus.Failed
                ? _fixture.Clock.GetUtcNow().UtcDateTime
                : null,
        };
        _fixture.DbContext.TicketPrintBatches.Add(batch);

        for (var i = 1; i <= ticketCount; i++)
        {
            _fixture.DbContext.Tickets.Add(
                Ticket.ForPrint(sector.Id, null, batchId, _productId, 80, i, validDate));
        }

        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return batch;
    }

    private async Task<TicketPrintBatch> ReloadAsync(Guid batchId)
    {
        _fixture.DbContext.ChangeTracker.Clear();
        return await _fixture.DbContext.TicketPrintBatches.AsNoTracking().FirstAsync(b => b.Id == batchId);
    }

    private async Task<byte[]> FileOfAsync(Guid batchId)
    {
        _fixture.DbContext.ChangeTracker.Clear();
        var file = await _fixture.DbContext.Set<TicketPrintBatchFile>().AsNoTracking().FirstAsync(f => f.BatchId == batchId);
        return file.Content;
    }

    private async Task<string> ExtractTextAsync(Guid batchId)
    {
        using var document = PdfDocument.Open(await FileOfAsync(batchId));
        return string.Join(" ", document.GetPages().Select(p => p.Text));
    }

    private static string Collapse(string value) => new([.. value.Where(c => !char.IsWhiteSpace(c))]);

    public void Dispose() => _fixture.Dispose();
}
