using System.Net;

namespace eTicketing.Notifications.Email.Templates;

public sealed record OrganizationCreatedData(string OrganizationName, string LoginUrl);

public static class OrganizationCreatedTemplate
{
    public static (string Subject, string Html) Render(OrganizationCreatedData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        var subject = $"Organizacija '{data.OrganizationName}' je kreirana — eKarta";

        var body = $"""
            <p>Poštovani,</p>
            <p>Obavještavamo vas da je na eKarta platformi kreirana nova organizacija:</p>
            <p style="font-size:18px;font-weight:700;color:#111827;">{WebUtility.HtmlEncode(data.OrganizationName)}</p>
            <p>Nalog Super Administratora organizacije je već kreiran i spreman za prijavu.</p>
            <p style="text-align:center;margin:24px 0;">
              <a href="{WebUtility.HtmlEncode(data.LoginUrl)}" style="display:inline-block;padding:12px 24px;background-color:#1f2937;
                        color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;">Prijavite se</a>
            </p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
