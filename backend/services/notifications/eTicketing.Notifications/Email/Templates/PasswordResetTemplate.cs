namespace eTicketing.Notifications.Email.Templates;

/// <summary>ResetLink always points at the web app (see FrontendOptions/NotificationDispatcher)
/// — mobile has no deep-linking, so a reset requested from the mobile app still completes in the
/// phone's browser via this same link.</summary>
public sealed record PasswordResetData(string FirstName, string ResetLink);

public static class PasswordResetTemplate
{
    public static (string Subject, string Html) Render(PasswordResetData data)
    {
        const string subject = "Resetujte svoju lozinku — eKarta";

        var body = $"""
            <p>Zdravo {data.FirstName},</p>
            <p>Zatražili ste resetovanje lozinke za vaš eKarta nalog. Kliknite na dugme ispod da postavite novu lozinku:</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{data.ResetLink}" style="display:inline-block;padding:12px 24px;background-color:#1f2937;
                        color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;">Resetujte lozinku</a>
            </p>
            <p>Link ističe za 1 sat. Ako niste vi zatražili resetovanje, slobodno ignorišite ovaj email — vaša lozinka ostaje nepromijenjena.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
