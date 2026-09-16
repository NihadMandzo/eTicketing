namespace eTicketing.Notifications.Email;

/// <summary>
/// A file attached by value: <paramref name="Content"/> is the raw file bytes, which
/// BrevoEmailSender base64-encodes into the request.
///
/// This used to be a URL for Brevo to fetch itself, which kept the bytes off the wire entirely.
/// That was abandoned on 2026-08-24 after live testing: Brevo accepted those requests with 201 and
/// reported them "delivered", but recipients never received them, while an otherwise identical
/// message carrying the same PDF as inline base64 arrived normally. Don't reintroduce the URL form
/// on the assumption it's cheaper — it silently loses mail.
/// </summary>
public sealed record EmailAttachment(string Name, byte[] Content);
