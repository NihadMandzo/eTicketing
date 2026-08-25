using System.Net;

namespace eTicketing.Notifications.Email.Templates;

/// <summary><paramref name="Field"/> arrives already translated (eTicketing.Catalog's
/// ProductChangeDetector resolves the Bosnian label, because the domain meaning of a field lives
/// in the service that owns it). This template never maps field names itself.</summary>
public sealed record ProductChangeLine(string Field, string? OldValue, string? NewValue);

public sealed record ProductChangedData(string ProductName, IReadOnlyList<ProductChangeLine> Changes);

public static class ProductChangedTemplate
{
    public static (string Subject, string Html) Render(ProductChangedData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        var subject = $"Izmjena informacija: '{data.ProductName}' — eKarta";

        var rows = string.Join("\n", data.Changes.Select(c => $"""
            <tr>
              <td style="padding:10px 0;border-bottom:1px solid #f3f4f6;vertical-align:top;">
                <strong>{WebUtility.HtmlEncode(c.Field)}</strong><br>
                <span style="color:#9ca3af;text-decoration:line-through;">{WebUtility.HtmlEncode(Display(c.OldValue))}</span><br>
                <span style="color:#111827;font-weight:600;">{WebUtility.HtmlEncode(Display(c.NewValue))}</span>
              </td>
            </tr>
            """));

        var body = $"""
            <p>Zdravo,</p>
            <p>Organizator je izmijenio informacije o događaju za koji imate ulaznicu:</p>
            <p style="font-size:18px;font-weight:700;color:#111827;">{WebUtility.HtmlEncode(data.ProductName)}</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0;">
              {rows}
            </table>
            <p>Vaša ulaznica i dalje vrijedi — nije potrebno ništa poduzimati. Molimo vas da provjerite nove
               informacije prije dolaska.</p>
            <p style="color:#6b7280;font-size:13px;">Najnovije podatke uvijek možete vidjeti u aplikaciji, u sekciji „Moje ulaznice“.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }

    /// <summary>A cleared optional field would otherwise render as an empty line with nothing to
    /// read on it — say so explicitly instead.</summary>
    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "(prazno)" : value;
}
