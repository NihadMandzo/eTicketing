using System.Net;

namespace eTicketing.Notifications.Email.Templates;

public sealed record OrganizationAdminDeletedData(string OrganizationName, string DeletedAdminFullName, string Reason);

public static class OrganizationAdminDeletedTemplate
{
    public static (string Subject, string Html) Render(OrganizationAdminDeletedData data)
    {
        // Deliberately NOT HTML-encoded — this is the real email Subject header, not markup
        // (EmailLayout.Wrap encodes its own copy of this string for the <title> tag).
        var subject = $"Administrator je uklonjen iz organizacije '{data.OrganizationName}'";

        // DeletedAdminFullName/OrganizationName come from user-registered fields; Reason is
        // SuperAdmin-entered free text up to 500 chars — none are character-restricted, and this
        // email goes to the organization's own contact address, a third party who never had a
        // chance to sanitize any of these values themselves.
        var body = $"""
            <p>Poštovani,</p>
            <p>Obavještavamo vas da je administratorski nalog <strong>{WebUtility.HtmlEncode(data.DeletedAdminFullName)}</strong>
               uklonjen iz organizacije <strong>{WebUtility.HtmlEncode(data.OrganizationName)}</strong> od strane Super Administratora platforme.</p>
            <p style="margin:20px 0;padding:16px;background-color:#f9fafb;border-left:4px solid #1f2937;border-radius:4px;">
              <strong>Razlog:</strong><br>{WebUtility.HtmlEncode(data.Reason)}
            </p>
            <p>Ovaj email je poslan za vaše evidencije.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
