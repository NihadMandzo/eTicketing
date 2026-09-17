namespace eTicketing.Notifications.Sending;

/// <summary>Just the two values BrevoEmailSender needs to build the "sender" field of a Brevo
/// request — deliberately not the full BrevoOptions (which also carries ApiKey), so this class
/// has no path to the API key value at all, not even through a shared options object.</summary>
public sealed record BrevoOptionsSnapshot(string SenderEmail, string SenderName);
