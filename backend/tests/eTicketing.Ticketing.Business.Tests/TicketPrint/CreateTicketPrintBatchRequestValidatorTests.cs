using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Business.TicketPrint.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.TicketPrint;

/// <summary>
/// The cheap gate that runs before TicketPrintService touches the database. Every rule here is
/// mirrored in the desktop export screen's form, per .claude/rules/00-workflow-and-testing.md —
/// this copy stays authoritative.
/// </summary>
public class CreateTicketPrintBatchRequestValidatorTests
{
    private readonly CreateTicketPrintBatchRequestValidator _sut = new();

    [Fact]
    public void Validate_ForAWellFormedRequest_Passes()
    {
        var result = _sut.Validate(Request((Guid.NewGuid(), null, 25)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithoutAProduct_Fails()
    {
        var request = Request((Guid.NewGuid(), null, 5)) with { ProductId = Guid.Empty };

        var result = _sut.Validate(request);

        result.Errors.Should().Contain(e => e.ErrorMessage == "Proizvod je obavezan.");
    }

    [Fact]
    public void Validate_WithNoLines_Fails()
    {
        var result = _sut.Validate(Request());

        result.Errors.Should().Contain(e => e.ErrorMessage == "Odaberite barem jedan sektor i broj ulaznica.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Validate_WithANonPositiveQuantity_Fails(int quantity)
    {
        var result = _sut.Validate(Request((Guid.NewGuid(), null, quantity)));

        result.Errors.Should().Contain(e => e.ErrorMessage == "Broj ulaznica mora biti veći od 0.");
    }

    [Fact]
    public void Validate_WithoutASector_Fails()
    {
        var result = _sut.Validate(Request((Guid.Empty, null, 5)));

        result.Errors.Should().Contain(e => e.ErrorMessage == "Sektor je obavezan.");
    }

    [Fact]
    public void Validate_AtExactlyTheBatchCeiling_Passes()
    {
        var result = _sut.Validate(Request((Guid.NewGuid(), null, TicketPrintService.MaxTicketsPerBatch)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OneOverTheBatchCeiling_Fails()
    {
        var result = _sut.Validate(Request((Guid.NewGuid(), null, TicketPrintService.MaxTicketsPerBatch + 1)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenTheLinesTogetherExceedTheCeiling_Fails()
    {
        // Each line is individually legal; only the sum is not. Without this rule an organizer
        // could queue an unbounded render by splitting it across sectors.
        var half = TicketPrintService.MaxTicketsPerBatch / 2;

        var result = _sut.Validate(Request(
            (Guid.NewGuid(), null, half + 1),
            (Guid.NewGuid(), null, half + 1)));

        result.Errors.Should().Contain(e =>
            e.ErrorMessage == $"Jedan izvoz može sadržavati najviše {TicketPrintService.MaxTicketsPerBatch} ulaznica.");
    }

    [Fact]
    public void Validate_WithTheSameSectorAndTicketTypeTwice_Fails()
    {
        var sectorId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var result = _sut.Validate(Request((sectorId, ticketTypeId, 10), (sectorId, ticketTypeId, 15)));

        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Isti sektor i vrsta ulaznice se ne mogu navesti dva puta.");
    }

    [Fact]
    public void Validate_WithTheSameSectorButDifferentTicketTypes_Passes()
    {
        // Two price tiers in one sector is the normal case, not a duplicate.
        var sectorId = Guid.NewGuid();

        var result = _sut.Validate(Request(
            (sectorId, Guid.NewGuid(), 10),
            (sectorId, Guid.NewGuid(), 15)));

        result.IsValid.Should().BeTrue();
    }

    private static CreateTicketPrintBatchRequest Request(
        params (Guid SectorId, Guid? TicketTypeId, int Quantity)[] lines) => new()
        {
            ProductId = Guid.NewGuid(),
            Lines = [.. lines.Select(l => new TicketPrintLineRequest
            {
                SectorId = l.SectorId,
                TicketTypeId = l.TicketTypeId,
                Quantity = l.Quantity,
            })],
        };
}
