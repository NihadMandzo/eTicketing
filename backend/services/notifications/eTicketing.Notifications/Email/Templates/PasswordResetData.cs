namespace eTicketing.Notifications.Email.Templates;

/// <summary>ResetLink always points at the web app (see FrontendOptions/NotificationDispatcher)
/// — mobile has no deep-linking, so a reset requested from the mobile app still completes in the
/// phone's browser via this same link.</summary>
public sealed record PasswordResetData(string FirstName, string ResetLink);
