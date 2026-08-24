using System.Net;

namespace eTicketing.Notifications.Email.Templates;

/// <summary>One row per ticket in the order — the PDFs themselves ride as attachments (see
/// EmailAttachment), this table is just so the buyer can tell at a glance what they got without
/// opening three files.</summary>
public sealed record TicketsReadyLine(string TicketCode, string SectorName, string? TicketTypeName, decimal PricePaid);

public sealed record TicketsReadyData(
    string ProductName,
    string ValidityLine,
    string City,
    decimal TotalPaid,
    IReadOnlyList<TicketsReadyLine> Tickets);

public static class TicketsReadyTemplate
{
    public static (string Subject, string Html) Render(TicketsReadyData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        var subject = $"Vaše ulaznice za '{data.ProductName}' — eKarta";

        var ticketCount = data.Tickets.Count;
        var attachmentNote = ticketCount == 1
            ? "Ulaznica je u prilogu ovog emaila kao PDF."
            : $"Sve ulaznice ({ticketCount}) su u prilogu ovog emaila, svaka kao zaseban PDF.";

        var rows = string.Join("\n", data.Tickets.Select(t => $"""
            <tr>
              <td style="padding:8px 0;border-bottom:1px solid #f3f4f6;">
                <span style="font-family:monospace;font-size:12px;">{WebUtility.HtmlEncode(t.TicketCode)}</span><br>
                <span style="font-size:12px;color:#6b7280;">{FormatSector(t)}</span>
              </td>
              <td style="padding:8px 0;border-bottom:1px solid #f3f4f6;text-align:right;white-space:nowrap;">
                {t.PricePaid:0.00} KM
              </td>
            </tr>
            """));

        var body = $"""
            <p>Zdravo,</p>
            <p>Vaša kupovina je uspješno završena. Hvala vam!</p>
            <p style="font-size:18px;font-weight:700;color:#111827;margin-bottom:4px;">{WebUtility.HtmlEncode(data.ProductName)}</p>
            <p style="margin-top:0;color:#6b7280;">{WebUtility.HtmlEncode(data.ValidityLine)} · {WebUtility.HtmlEncode(data.City)}</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:20px 0;">
              {rows}
              <tr>
                <td style="padding:10px 0;font-weight:700;">Ukupno</td>
                <td style="padding:10px 0;text-align:right;font-weight:700;white-space:nowrap;">{data.TotalPaid:0.00} KM</td>
              </tr>
            </table>
            <p>{attachmentNote} Svaka ulaznica ima svoj QR kod koji se skenira na ulazu i vrijedi za jedan ulaz.</p>
            <p style="color:#6b7280;font-size:13px;">Ulaznice možete pogledati i u aplikaciji, u sekciji „Moje ulaznice“.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }

    /// <summary>Encodes the two untrusted (organizer-supplied) halves separately and joins them
    /// with a literal separator, rather than encoding the joined string — otherwise HtmlEncode
    /// turns the middle dot into a "&amp;#183;" numeric entity, which renders fine but makes the
    /// markup needlessly opaque.</summary>
    private static string FormatSector(TicketsReadyLine line)
    {
        var sector = WebUtility.HtmlEncode(line.SectorName);
        return line.TicketTypeName is null ? sector : $"{sector} · {WebUtility.HtmlEncode(line.TicketTypeName)}";
    }
}
