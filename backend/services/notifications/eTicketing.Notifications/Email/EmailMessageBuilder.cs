using eTicketing.Notifications.Email.Templates;

namespace eTicketing.Notifications.Email;

/// <summary>Fluent assembler for an outbound EmailMessage. Lives entirely inside this service —
/// not a shared library — per the confirmed design decision for this feature.</summary>
public sealed class EmailMessageBuilder
{
    private string? _toEmail;
    private string? _toName;
    private string? _subject;
    private string? _htmlBody;

    public EmailMessageBuilder WithTo(string email, string? name = null)
    {
        _toEmail = email;
        _toName = name;
        return this;
    }

    public EmailMessageBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    /// <summary>Each EmailTemplate value has exactly one paired *Data record — a wrong pairing
    /// throws InvalidCastException immediately (caught by that template's own unit test).</summary>
    public EmailMessageBuilder WithTemplate(EmailTemplate template, object data)
    {
        var (subject, html) = template switch
        {
            EmailTemplate.VerificationEmail => VerificationEmailTemplate.Render((VerificationEmailData)data),
            EmailTemplate.OrganizationCreated => OrganizationCreatedTemplate.Render((OrganizationCreatedData)data),
            EmailTemplate.OrganizationAdminDeleted => OrganizationAdminDeletedTemplate.Render((OrganizationAdminDeletedData)data),
            EmailTemplate.PasswordReset => PasswordResetTemplate.Render((PasswordResetData)data),
            EmailTemplate.AdminPasswordChanged => AdminPasswordChangedTemplate.Render((AdminPasswordChangedData)data),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Nepoznat email template.")
        };

        // WithSubject, when called, wins over the template's default — but a template call
        // always sets the body, so subject only falls back to the template's default if nothing
        // more specific was ever provided.
        _subject ??= subject;
        _htmlBody = html;
        return this;
    }

    public EmailMessage Build()
    {
        if (_toEmail is null)
            throw new InvalidOperationException("WithTo mora biti pozvan prije Build().");
        if (_htmlBody is null)
            throw new InvalidOperationException("WithTemplate mora biti pozvan prije Build().");

        return new EmailMessage(_toEmail, _toName, _subject ?? string.Empty, _htmlBody);
    }
}
