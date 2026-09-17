using System.Net;

namespace eTicketing.Notifications.Email.Templates;

public static class VerificationEmailTemplate
{
    public static (string Subject, string Html) Render(VerificationEmailData data)
    {
        const string subject = "Potvrdite svoju email adresu — eKarta";

        // FirstName is user-supplied at registration (length-checked only, not
        // character-restricted) — HTML-encoded here so it can never break the email's markup
        // or inject content into a transactional email.
        var body = $"""
            <p>Zdravo {WebUtility.HtmlEncode(data.FirstName)},</p>
            <p>Hvala na registraciji. Unesite sljedeći kod na stranici za potvrdu email adrese:</p>
            <p style="text-align:center;margin:24px 0;">
              <span style="display:inline-block;padding:12px 24px;background-color:#f3f4f6;border-radius:6px;
                           font-size:24px;font-weight:700;letter-spacing:4px;color:#111827;">{WebUtility.HtmlEncode(data.VerificationCode)}</span>
            </p>
            <p>Kod ističe za 24 sata. Ako niste vi zatražili registraciju, slobodno ignorišite ovaj email.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
