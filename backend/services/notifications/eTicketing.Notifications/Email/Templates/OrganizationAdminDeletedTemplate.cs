namespace eTicketing.Notifications.Email.Templates;

public sealed record OrganizationAdminDeletedData(string OrganizationName, string DeletedAdminFullName, string Reason);

public static class OrganizationAdminDeletedTemplate
{
    public static (string Subject, string Html) Render(OrganizationAdminDeletedData data)
    {
        var subject = $"Administrator je uklonjen iz organizacije '{data.OrganizationName}'";

        var body = $"""
            <p>Poštovani,</p>
            <p>Obavještavamo vas da je administratorski nalog <strong>{data.DeletedAdminFullName}</strong>
               uklonjen iz organizacije <strong>{data.OrganizationName}</strong> od strane Super Administratora platforme.</p>
            <p style="margin:20px 0;padding:16px;background-color:#f9fafb;border-left:4px solid #1f2937;border-radius:4px;">
              <strong>Razlog:</strong><br>{data.Reason}
            </p>
            <p>Ovaj email je poslan za vaše evidencije.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
