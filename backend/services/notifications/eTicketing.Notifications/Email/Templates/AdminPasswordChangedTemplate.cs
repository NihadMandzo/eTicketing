using System.Net;

namespace eTicketing.Notifications.Email.Templates;

public sealed record AdminPasswordChangedData(string FirstName);

public static class AdminPasswordChangedTemplate
{
    public static (string Subject, string Html) Render(AdminPasswordChangedData data)
    {
        const string subject = "Vaša lozinka je promijenjena od strane administratora";

        var body = $"""
            <p>Zdravo {WebUtility.HtmlEncode(data.FirstName)},</p>
            <p>Super Administrator platforme je upravo postavio novu lozinku za vaš nalog.</p>
            <p>Prilikom sljedeće prijave bit ćete zamoljeni da postavite vlastitu novu lozinku.</p>
            <p>Ako ovo niste očekivali, odmah kontaktirajte podršku.</p>
            """;

        return (subject, EmailLayout.Wrap(subject, body));
    }
}
