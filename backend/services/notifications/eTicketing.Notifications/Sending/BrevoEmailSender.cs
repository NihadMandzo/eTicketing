using System.Diagnostics;
using System.Net.Http.Json;
using eTicketing.Notifications.Email;
using Microsoft.Extensions.Logging;

namespace eTicketing.Notifications.Sending;

/// <summary>Sends via Brevo's transactional email HTTP API (POST /v3/smtp/email), not SMTP.
/// The api-key header is configured once at HttpClient registration time (see
/// NotificationsServiceCollectionExtensions) — it is never touched, read, or logged anywhere in
/// this class, satisfying the "Brevo API key is never logged" requirement by construction rather
/// than by discipline.</summary>
public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoOptionsSnapshot _sender;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient httpClient, BrevoOptionsSnapshot sender, ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _sender = sender;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var payload = new BrevoSendRequest(
            new BrevoContact(_sender.SenderEmail, _sender.SenderName),
            [new BrevoContact(message.ToEmail, message.ToName)],
            message.Subject,
            message.HtmlBody,
            // null, not an empty array: Brevo rejects "attachment": [] as a malformed request,
            // which would break every email that has no attachment (i.e. most of them).
            message.Attachments.Count == 0
                ? null
                : message.Attachments.Select(a => new BrevoAttachment(Convert.ToBase64String(a.Content), a.Name)).ToArray());

        var stopwatch = Stopwatch.StartNew();
        // No custom logging handler/message-logging enabled on this HttpClient (see DI wiring) —
        // deliberately, so nothing in the default request/response pipeline can echo the api-key
        // header (set as a default request header on this client, never touched here) into logs.
        using var response = await _httpClient.PostAsJsonAsync("smtp/email", payload, ct);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Brevo email slanje neuspješno. Status: {StatusCode}, Trajanje: {ElapsedMs}ms.",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            throw new InvalidOperationException($"Brevo API je vratio status {(int)response.StatusCode}.");
        }
    }

    private sealed record BrevoSendRequest(
        BrevoContact Sender, BrevoContact[] To, string Subject, string HtmlContent, BrevoAttachment[]? Attachment);

    private sealed record BrevoContact(string Email, string? Name);

    /// <summary>Brevo's inline attachment form: Content is the base64-encoded file. Property names
    /// are lowercase on the wire ("content"/"name") — matched by the camelCase serializer
    /// PostAsJsonAsync uses by default.
    ///
    /// Deliberately NOT Brevo's alternative {"url": ...} form, which has it fetch the file itself.
    /// That was the original implementation and it silently lost mail: Brevo returned 201 and
    /// logged the message as "delivered", but it never reached the recipient, while the same PDF
    /// sent inline arrived normally (verified live against Brevo on 2026-08-24).</summary>
    private sealed record BrevoAttachment(string Content, string Name);
}
