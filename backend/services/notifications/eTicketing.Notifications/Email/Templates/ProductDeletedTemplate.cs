using System.Globalization;
using System.Net;
using eTicketing.Contracts.Events;

namespace eTicketing.Notifications.Email.Templates;

/// <summary>
/// One template, two audiences — see <see cref="ProductDeletedAudience"/>. A buyer is told their
/// ticket is void and who to ask for their money back; an organization is told the platform
/// removed one of its products and how many buyers it now owes refunds to.
/// </summary>
/// <param name="OrganizerEmail">Null when Identity had no contact on file or was unreachable. The
/// contact block is then omitted entirely rather than printing an empty "Email:" line.</param>
public sealed record ProductDeletedData(
    string ProductName,
    ProductDeletedAudience Audience,
    DateTime? ProductDate,
    string OrganizerName,
    string? OrganizerEmail,
    string? OrganizerPhone,
    int TicketCount);

public static class ProductDeletedTemplate
{
    public static (string Subject, string Html) Render(ProductDeletedData data) =>
        data.Audience == ProductDeletedAudience.Organizer ? RenderForOrganizer(data) : RenderForBuyer(data);

    private static (string Subject, string Html) RenderForBuyer(ProductDeletedData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        var subject = $"Otkazano: '{data.ProductName}' — eKarta";

        var body = $"""
            <p>Poštovani,</p>
            <p>Nažalost, događaj za koji imate {TicketPhrase(data.TicketCount)} je otkazan i uklonjen sa platforme:</p>
            {EventBlock(data)}
            <p><strong>Vaša ulaznica više ne vrijedi</strong> i neće biti prihvaćena na ulazu.</p>
            {RefundBlock(data)}
            <p style="color:#6b7280;font-size:13px;">Povrat novca obrađuje organizator, ne eKarta —
               naplata je izvršena u njegovo ime.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }

    private static (string Subject, string Html) RenderForOrganizer(ProductDeletedData data)
    {
        var subject = $"Vaš događaj je uklonjen: '{data.ProductName}' — eKarta";

        // Buyers are told separately and automatically; saying so here stops an organizer sending
        // a second round of "we're sorry" emails to the same people.
        var buyersLine = data.TicketCount == 0
            ? "<p>Ovaj događaj nije imao aktivnih kupaca, pa nikome nije poslano obavještenje.</p>"
            : $"""
              <p style="margin:20px 0;padding:16px;background-color:#fef2f2;border-left:4px solid #dc2626;border-radius:4px;">
                <strong>{data.TicketCount}</strong> {BuyerWord(data.TicketCount)} sa važećim ulaznicama je automatski
                obaviješteno da je događaj otkazan i upućeno na vas radi povrata novca.
              </p>
              """;

        var body = $"""
            <p>Poštovani,</p>
            <p>Obavještavamo vas da je administrator platforme uklonio sljedeći događaj vaše organizacije:</p>
            {EventBlock(data)}
            {buyersLine}
            <p>Ako smatrate da je došlo do greške, kontaktirajte podršku platforme.</p>
            <p>Ovaj email je poslan za vaše evidencije.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }

    private static string EventBlock(ProductDeletedData data)
    {
        // Products with no single date (DailyEntry day passes, RecurringReservation spaces) render
        // the name alone rather than an empty date line — see Category.TicketingMode.
        var dateLine = data.ProductDate is null
            ? string.Empty
            : $"""<br><span style="color:#6b7280;font-size:14px;">{WebUtility.HtmlEncode(FormatDate(data.ProductDate.Value))}</span>""";

        return $"""
            <p style="font-size:18px;font-weight:700;color:#111827;margin:16px 0;">
              {WebUtility.HtmlEncode(data.ProductName)}{dateLine}
            </p>
            """;
    }

    private static string RefundBlock(ProductDeletedData data)
    {
        var rows = new List<string>();

        if (!string.IsNullOrWhiteSpace(data.OrganizerEmail))
        {
            rows.Add($"""<br>Email: <a href="mailto:{WebUtility.HtmlEncode(data.OrganizerEmail)}">{WebUtility.HtmlEncode(data.OrganizerEmail)}</a>""");
        }

        if (!string.IsNullOrWhiteSpace(data.OrganizerPhone))
        {
            rows.Add($"<br>Telefon: {WebUtility.HtmlEncode(data.OrganizerPhone)}");
        }

        // No contact on file: still say who to chase, just without the how — better than a block
        // with a blank line where the address should be.
        var contactLines = rows.Count == 0 ? string.Empty : string.Join("\n", rows);

        return $"""
            <p style="margin:20px 0;padding:16px;background-color:#f9fafb;border-left:4px solid #1f2937;border-radius:4px;">
              <strong>Za povrat novca kontaktirajte organizatora:</strong><br>
              {WebUtility.HtmlEncode(data.OrganizerName)}{contactLines}
            </p>
            """;
    }

    /// <summary>Bosnian plural for the buyer's own ticket count — "1 ulaznicu" vs "3 ulaznice".</summary>
    private static string TicketPhrase(int count) => count == 1
        ? "ulaznicu"
        : $"{count} ulaznice";

    private static string BuyerWord(int count) => count == 1 ? "kupac" : "kupaca";

    private static string FormatDate(DateTime date) =>
        date.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture);
}
