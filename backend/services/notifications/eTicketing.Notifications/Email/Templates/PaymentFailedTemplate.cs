using System.Globalization;
using System.Net;

namespace eTicketing.Notifications.Email.Templates;

/// <param name="ReasonCode">The payment provider's raw failure code. The template decides what, if
/// anything, to say about it — see <see cref="PaymentFailedTemplate.DescribeReason"/>.</param>
public sealed record PaymentFailedData(
    Guid? OrderId,
    string? SectorName,
    decimal? Amount,
    string ReasonCode,
    DateTime FailedAt);

/// <summary>
/// "Your card was declined." Nothing was captured — the purchase path releases the hold and cancels
/// the authorization before this event is published — so this email deliberately carries no refund
/// instructions, no "we will retry", and no amount owed. The only useful action is to try again.
/// </summary>
public static class PaymentFailedTemplate
{
    public static (string Subject, string Html) Render(PaymentFailedData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        const string subject = "Plaćanje nije uspjelo — eKarta";

        var body = $"""
            <p>Poštovani,</p>
            <p>Vaše plaćanje nije uspjelo i ulaznice nisu izdate.</p>
            {OrderBlock(data)}
            <p style="margin:20px 0;padding:16px;background-color:#fef2f2;border-left:4px solid #dc2626;border-radius:4px;">
              {WebUtility.HtmlEncode(DescribeReason(data.ReasonCode))}
            </p>
            <p><strong>Novac vam nije naplaćen.</strong> Rezervacija je oslobođena, pa su mjesta ponovo
               dostupna — i vama i ostalim kupcima.</p>
            <p>Ako želite nastaviti, pokušajte ponovo u aplikaciji. Ulaznice se izdaju tek kada plaćanje uspije.</p>
            <p style="color:#6b7280;font-size:13px;">Ako se isti problem ponovi, obratite se svojoj banci —
               eKarta ne vidi razlog odbijanja izvan poruke iznad.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }

    /// <summary>
    /// The order details, omitted entirely when absent rather than printed as empty rows. They are
    /// nullable only for the deploy window — an in-flight event from the previous version carries
    /// none of them, and a buyer getting a slightly vaguer email beats a dead-lettered one.
    /// </summary>
    private static string OrderBlock(PaymentFailedData data)
    {
        var rows = new List<string>();

        if (!string.IsNullOrWhiteSpace(data.SectorName))
            rows.Add(Row("Sektor", data.SectorName));

        if (data.Amount is not null)
            rows.Add(Row("Iznos", $"{data.Amount.Value.ToString("0.00", CultureInfo.InvariantCulture)} KM"));

        if (data.OrderId is not null)
        {
            // The same short, readable form the ticket rows use, so a buyer quoting it to support
            // is quoting something a human can compare by eye.
            rows.Add(Row("Broj narudžbe", data.OrderId.Value.ToString("N")[..8].ToUpperInvariant()));
        }

        rows.Add(Row("Vrijeme", data.FailedAt.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture)));

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0;">
              {string.Join("\n", rows)}
            </table>
            """;
    }

    private static string Row(string label, string value) => $"""
        <tr>
          <td style="padding:8px 0;border-bottom:1px solid #f3f4f6;color:#6b7280;width:40%;">{WebUtility.HtmlEncode(label)}</td>
          <td style="padding:8px 0;border-bottom:1px solid #f3f4f6;color:#111827;font-weight:600;">{WebUtility.HtmlEncode(value)}</td>
        </tr>
        """;

    /// <summary>
    /// Turns the provider's failure code into something a person can act on.
    ///
    /// <para>Two reasons this mapping exists rather than printing <c>Reason</c> straight through.
    /// The obvious one: <c>insufficient_funds</c> is not Bosnian and not a sentence. The one that
    /// matters more: a handful of codes describe the <i>card</i> rather than the transaction —
    /// reported lost, reported stolen, flagged as fraudulent — and the address on an order is not
    /// necessarily the cardholder's. Those all fall through to the generic wording on purpose;
    /// telling the wrong person that a card has been reported stolen confirms something for them.
    /// The raw code stays on the event either way, so the detail is in the logs, not the inbox.</para>
    /// </summary>
    internal static string DescribeReason(string reasonCode) => reasonCode switch
    {
        "insufficient_funds" => "Na računu nema dovoljno sredstava.",
        "expired_card" => "Kartica je istekla. Pokušajte sa drugom karticom.",
        "incorrect_cvc" or "invalid_cvc" => "Sigurnosni kod (CVC) nije ispravan.",
        "incorrect_number" or "invalid_number" => "Broj kartice nije ispravan.",
        "invalid_expiry_month" or "invalid_expiry_year" => "Datum isteka kartice nije ispravan.",
        "authentication_required" => "Banka traži dodatnu potvrdu (3D Secure). Pokušajte ponovo i potvrdite plaćanje kod svoje banke.",
        "card_not_supported" or "currency_not_supported" => "Ova kartica ne podržava ovakvo plaćanje.",
        "processing_error" => "Došlo je do greške pri obradi kod banke. Pokušajte ponovo za nekoliko minuta.",

        // Everything else — including lost_card, stolen_card, fraudulent, pickup_card and any code
        // the provider adds later — deliberately lands here. See the doc comment above.
        _ => "Banka je odbila plaćanje. Provjerite podatke kartice ili pokušajte sa drugom.",
    };
}
