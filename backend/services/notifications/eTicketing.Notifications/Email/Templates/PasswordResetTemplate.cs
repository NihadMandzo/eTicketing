using System.Net;

namespace eTicketing.Notifications.Email.Templates;

public static class PasswordResetTemplate
{
    public static (string Subject, string Html) Render(PasswordResetData data)
    {
        const string subject = "Resetujte svoju lozinku — eKarta";

        // FirstName is user-supplied (not character-restricted); ResetLink is system-built but
        // still HTML-encoded since it sits in an href attribute (a literal "&" between query
        // params would otherwise render incorrectly there).
        var body = $"""
            <p>Zdravo {WebUtility.HtmlEncode(data.FirstName)},</p>
            <p>Zatražili ste resetovanje lozinke za vaš eKarta nalog. Kliknite na dugme ispod da postavite novu lozinku:</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(data.ResetLink)}" style="display:inline-block;padding:12px 24px;background-color:#1f2937;
                        color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;">Resetujte lozinku</a>
            </p>
            <p>Link ističe za 1 sat. Ako niste vi zatražili resetovanje, slobodno ignorišite ovaj email — vaša lozinka ostaje nepromijenjena.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
