using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Mapster;

namespace eTicketing.Ticketing.Business.Tests.TicketPrint;

/// <summary>
/// Mapster's failure mode is silence: a destination member with no matching source member is left
/// at its default and nothing anywhere complains. On a sixteen-member response that is a very quiet
/// way to start returning zeroes to an organizer counting printed money.
///
/// <para>So these tests do two things a hand-written projection did not need. They compile the whole
/// registered config, which fails loudly if any <see cref="IRegister"/> in this assembly references
/// a member that no longer exists. And they sweep the destination by reflection, so a member added
/// to the response later — without a matching source member — fails here rather than shipping as a
/// blank column.</para>
/// </summary>
public class TicketPrintMappingTests
{
    private static TicketPrintBatch Batch() => new()
    {
        Id = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        RequestedByUserId = Guid.NewGuid(),
        ProductName = "Ljetni Festival",
        Status = TicketPrintBatchStatus.Ready,
        TicketCount = 500,
        RenderedCount = 500,
        PageCount = 63,
        SerialFrom = 1001,
        SerialTo = 1500,
        NominalValue = 25_000m,
        ValidDate = new DateOnly(2026, 9, 1),
        FileSizeBytes = 4_194_304,
        ErrorMessage = "nije bilo greške",
        CompletedAt = new DateTime(2026, 8, 24, 11, 0, 0, DateTimeKind.Utc),
        DownloadedAt = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void GlobalConfig_Compiles()
    {
        // Catches a config that names a member that has since been renamed or removed — the one
        // Mapster mistake that is otherwise invisible until a response comes back half empty.
        var compile = () => TypeAdapterConfig.GlobalSettings.Compile();

        compile.Should().NotThrow();
    }

    [Fact]
    public void Batch_MapsEveryFieldTheOrganizerReads()
    {
        var batch = Batch();

        var response = batch.Adapt<TicketPrintBatchResponse>();

        response.Id.Should().Be(batch.Id);
        response.ProductId.Should().Be(batch.ProductId);
        response.ProductName.Should().Be("Ljetni Festival");
        response.Status.Should().Be(TicketPrintBatchStatus.Ready);
        response.NominalValue.Should().Be(25_000m);
        response.ValidDate.Should().Be(new DateOnly(2026, 9, 1));
        response.FileSizeBytes.Should().Be(4_194_304);
        response.ErrorMessage.Should().Be("nije bilo greške");
        response.CreatedAt.Should().Be(batch.CreatedAt);
        response.CompletedAt.Should().Be(batch.CompletedAt);
        response.DownloadedAt.Should().Be(batch.DownloadedAt);
    }

    [Fact]
    public void Batch_DoesNotTransposeThePairsThatLookAlike()
    {
        // SerialFrom/SerialTo and TicketCount/RenderedCount are same-typed neighbours: swapping
        // either pair compiles, reads plausibly, and shows an organizer a progress bar that is
        // finished before it started or a serial range printed backwards.
        var batch = Batch();
        batch.TicketCount = 500;
        batch.RenderedCount = 120;

        var response = batch.Adapt<TicketPrintBatchResponse>();

        response.SerialFrom.Should().Be(1001);
        response.SerialTo.Should().Be(1500);
        response.TicketCount.Should().Be(500);
        response.RenderedCount.Should().Be(120);
        response.PageCount.Should().Be(63);
    }

    [Fact]
    public void Batch_LeavesNoResponseMemberAtItsDefault()
    {
        // The guard against a member added to the response later with no source to fill it. Every
        // value in Batch() is deliberately non-default, so anything still default here was not
        // mapped at all.
        var response = Batch().Adapt<TicketPrintBatchResponse>();

        var unmapped = typeof(TicketPrintBatchResponse)
            .GetProperties()
            .Where(p => IsDefault(p.PropertyType, p.GetValue(response)))
            .Select(p => p.Name)
            .ToList();

        unmapped.Should().BeEmpty("every member of the response must come from the batch");
    }

    /// <summary>Null for anything, plus the zero value for value types. Reference types other than
    /// null cannot be compared to a "default instance" — string has no parameterless constructor —
    /// so for those, null is the whole test.</summary>
    private static bool IsDefault(Type propertyType, object? value)
    {
        if (value is null)
            return true;

        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return type.IsValueType && value.Equals(Activator.CreateInstance(type));
    }
}
