namespace eTicketing.Notifications.Options;

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    /// <summary>Never logged anywhere — see BrevoEmailSender's logging rules.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "eKarta";
}
