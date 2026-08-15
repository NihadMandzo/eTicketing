namespace eTicketing.Notifications.Email.Templates;

public sealed record VerificationEmailData(string FirstName, string VerificationCode);

public static class VerificationEmailTemplate
{
    public static (string Subject, string Html) Render(VerificationEmailData data)
    {
        const string subject = "Potvrdite svoju email adresu — eKarta";

        var body = $"""
            <p>Zdravo {data.FirstName},</p>
            <p>Hvala na registraciji. Unesite sljedeći kod na stranici za potvrdu email adrese:</p>
            <p style="text-align:center;margin:24px 0;">
              <span style="display:inline-block;padding:12px 24px;background-color:#f3f4f6;border-radius:6px;
                           font-size:24px;font-weight:700;letter-spacing:4px;color:#111827;">{data.VerificationCode}</span>
            </p>
            <p>Kod ističe za 24 sata. Ako niste vi zatražili registraciju, slobodno ignorišite ovaj email.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
