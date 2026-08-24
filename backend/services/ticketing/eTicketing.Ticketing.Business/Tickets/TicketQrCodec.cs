using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// Mints and verifies the string a ticket's QR code actually encodes:
/// <c>ETK1.{ticketId:N}.{signature}</c>, where signature is the first 16 bytes of
/// HMAC-SHA256("ETK1.{ticketId:N}") under a service-local key, base64url-encoded.
///
/// The signature is not what makes a ticket valid — <see cref="TicketValidationService"/>'s DB
/// checks (exists, belongs to this product, belongs to this organization, not already used, valid
/// today) are. It exists so a code that never came out of this service is rejected before any of
/// that runs, and so ticket ids can't be probed by scanning crafted QR images at the gate.
///
/// Only eTicketing.Ticketing ever holds the key: the finished payload rides on TicketPurchased, so
/// eTicketing.PdfGeneration renders a QR from a string it's handed and never signs anything.
/// </summary>
public sealed class TicketQrCodec
{
    private const string Prefix = "ETK1";
    private const int SignatureBytes = 16;

    private readonly byte[] _key;

    public TicketQrCodec(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("Qr:SigningKey mora biti podešen.", nameof(signingKey));

        _key = Encoding.UTF8.GetBytes(signingKey);
    }

    public string Sign(Guid ticketId)
    {
        var body = Body(ticketId);
        return $"{body}.{Signature(body)}";
    }

    /// <summary>
    /// Accepts either a full signed payload (what a camera scan produces) or a bare ticket GUID
    /// (what the scanner's "unesi kod ručno" fallback produces). The bare form is deliberately
    /// allowed: that path is already behind an authenticated Organizer token typed by trusted gate
    /// staff, and the id still has to survive every DB check afterwards — so requiring a 60-character
    /// signature to be typed by hand would buy nothing and cost a working fallback.
    /// </summary>
    public bool TryParse(string? code, out Guid ticketId)
    {
        ticketId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        code = code.Trim();

        if (Guid.TryParse(code, out var bareId))
        {
            ticketId = bareId;
            return true;
        }

        var parts = code.Split('.');
        if (parts.Length != 3 || parts[0] != Prefix)
            return false;

        if (!Guid.TryParseExact(parts[1], "N", out var signedId))
            return false;

        // Fixed-time comparison — a QR payload is attacker-supplied input, and a timing oracle on
        // the signature is exactly the thing an HMAC is supposed to not have.
        var expected = Encoding.UTF8.GetBytes(Signature(Body(signedId)));
        var actual = Encoding.UTF8.GetBytes(parts[2]);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return false;

        ticketId = signedId;
        return true;
    }

    private static string Body(Guid ticketId) => $"{Prefix}.{ticketId:N}";

    private string Signature(string body)
    {
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(body));
        // Base64Url: no '+'/'/' to be mangled by a QR reader's charset heuristics, and no '=' padding
        // to be mistaken for a separator.
        return Base64Url.EncodeToString(hash.AsSpan(0, SignatureBytes));
    }
}
