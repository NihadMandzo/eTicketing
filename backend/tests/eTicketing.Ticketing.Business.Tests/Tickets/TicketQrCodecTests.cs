using eTicketing.Ticketing.Business.Tickets;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

public class TicketQrCodecTests
{
    private const string Key = "test-qr-signing-key-min-32-characters-long";
    private static readonly TicketQrCodec Sut = new(Key);

    [Fact]
    public void Sign_ThenTryParse_RoundTripsTheTicketId()
    {
        var ticketId = Guid.NewGuid();

        var payload = Sut.Sign(ticketId);

        Sut.TryParse(payload, out var parsed).Should().BeTrue();
        parsed.Should().Be(ticketId);
    }

    [Fact]
    public void Sign_ProducesTheDocumentedThreePartShape()
    {
        var ticketId = Guid.NewGuid();

        var parts = Sut.Sign(ticketId).Split('.');

        parts.Should().HaveCount(3);
        parts[0].Should().Be("ETK1");
        parts[1].Should().Be(ticketId.ToString("N"));
        parts[2].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Sign_IsDeterministic_SoTheSameTicketAlwaysScansTheSame()
    {
        // A PDF printed last week and the QR shown in the app today must be the same code — the
        // payload is never stored, only recomputed, so determinism is what keeps them in sync.
        var ticketId = Guid.NewGuid();

        Sut.Sign(ticketId).Should().Be(Sut.Sign(ticketId));
    }

    [Fact]
    public void TryParse_WithTamperedSignature_ReturnsFalse()
    {
        var payload = Sut.Sign(Guid.NewGuid());
        var parts = payload.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";

        Sut.TryParse(tampered, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithSignatureFromADifferentTicket_ReturnsFalse()
    {
        // The signature covers the ticket id, so lifting a valid signature onto someone else's
        // id must not produce a usable code.
        var mine = Sut.Sign(Guid.NewGuid()).Split('.');
        var theirs = Sut.Sign(Guid.NewGuid()).Split('.');
        var swapped = $"{mine[0]}.{mine[1]}.{theirs[2]}";

        Sut.TryParse(swapped, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithSignatureFromADifferentKey_ReturnsFalse()
    {
        var ticketId = Guid.NewGuid();
        var forged = new TicketQrCodec("a-completely-different-signing-key-32-chars").Sign(ticketId);

        Sut.TryParse(forged, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithBareTicketGuid_ReturnsTrue()
    {
        // The scanner's "unesi kod ručno" fallback: already behind an authenticated Organizer
        // token, and the id still has to survive every DB check afterwards.
        var ticketId = Guid.NewGuid();

        Sut.TryParse(ticketId.ToString(), out var parsed).Should().BeTrue();
        parsed.Should().Be(ticketId);
    }

    [Fact]
    public void TryParse_TrimsSurroundingWhitespace()
    {
        var ticketId = Guid.NewGuid();

        Sut.TryParse($"  {Sut.Sign(ticketId)}  ", out var parsed).Should().BeTrue();
        parsed.Should().Be(ticketId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-code")]
    [InlineData("ETK1.notaguid.signature")]
    [InlineData("ETK2.00000000000000000000000000000000.sig")]   // unknown version prefix
    [InlineData("ETK1.00000000000000000000000000000000")]        // missing signature segment
    [InlineData("https://example.com/some-other-qr")]
    public void TryParse_WithUnusableInput_ReturnsFalse(string? code)
    {
        Sut.TryParse(code, out var parsed).Should().BeFalse();
        parsed.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingKey_Throws(string? key)
    {
        // Fail at startup rather than mint QR codes nobody can verify.
        var act = () => new TicketQrCodec(key!);

        act.Should().Throw<ArgumentException>();
    }
}
