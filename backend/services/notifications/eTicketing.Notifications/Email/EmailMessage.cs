namespace eTicketing.Notifications.Email;

/// <summary>Fully assembled outbound email, ready for IEmailSender — the output of
/// EmailMessageBuilder.Build().</summary>
public sealed record EmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody);
