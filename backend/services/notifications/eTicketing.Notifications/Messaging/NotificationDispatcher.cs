using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Notifications.Email;
using eTicketing.Notifications.Email.Templates;
using eTicketing.Notifications.Options;
using eTicketing.Notifications.Sending;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eTicketing.Notifications.Messaging;

/// <summary>Routes a raw RabbitMQ message body to the matching email, by routing key. One
/// handler per event type, each building via EmailMessageBuilder and sending via IEmailSender.
/// Deserialization failures and unknown routing keys are wrapped in PoisonMessageException so
/// the consumer can tell them apart from a transient Brevo failure (see
/// RabbitMqConsumerService).</summary>
public sealed class NotificationDispatcher
{
    private readonly IEmailSender _emailSender;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IEmailSender emailSender, IOptions<FrontendOptions> frontendOptions, ILogger<NotificationDispatcher> logger)
    {
        _emailSender = emailSender;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public Task DispatchAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct = default) => routingKey switch
    {
        EventNames.VerificationEmailRequested => HandleVerificationEmailRequestedAsync(body, ct),
        EventNames.OrganizationCreated => HandleOrganizationCreatedAsync(body, ct),
        EventNames.OrganizationAdminDeleted => HandleOrganizationAdminDeletedAsync(body, ct),
        EventNames.PasswordResetRequested => HandlePasswordResetRequestedAsync(body, ct),
        EventNames.AdminPasswordChanged => HandleAdminPasswordChangedAsync(body, ct),
        _ => throw new PoisonMessageException($"Nepoznat routing key: {routingKey}")
    };

    private async Task HandleVerificationEmailRequestedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<VerificationEmailRequested>(body, EventNames.VerificationEmailRequested);
        _logger.LogInformation("Šaljem verifikacioni email korisniku {UserId}.", evt.UserId);

        var message = new EmailMessageBuilder()
            .WithTo(evt.Email, evt.FirstName)
            .WithTemplate(EmailTemplate.VerificationEmail, new VerificationEmailData(evt.FirstName, evt.VerificationCode))
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private async Task HandleOrganizationCreatedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<OrganizationCreatedNotification>(body, EventNames.OrganizationCreated);
        _logger.LogInformation("Šaljem obavještenje o kreiranju organizacije {OrganizationId}.", evt.OrganizationId);

        var loginUrl = $"{_frontendOptions.WebBaseUrl.TrimEnd('/')}/prijava";
        var message = new EmailMessageBuilder()
            .WithTo(evt.RecipientEmail)
            .WithTemplate(EmailTemplate.OrganizationCreated, new OrganizationCreatedData(evt.OrganizationName, loginUrl))
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private async Task HandleOrganizationAdminDeletedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<OrganizationAdminDeletedNotification>(body, EventNames.OrganizationAdminDeleted);
        _logger.LogInformation("Šaljem obavještenje o brisanju administratora organizacije {OrganizationId}.", evt.OrganizationId);

        var message = new EmailMessageBuilder()
            .WithTo(evt.RecipientEmail)
            .WithTemplate(EmailTemplate.OrganizationAdminDeleted,
                new OrganizationAdminDeletedData(evt.OrganizationName, evt.DeletedAdminFullName, evt.Reason))
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private async Task HandlePasswordResetRequestedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<PasswordResetRequested>(body, EventNames.PasswordResetRequested);
        // Deliberately logs only the user id — never evt.ResetToken, and never the assembled
        // resetLink below (it embeds the raw token as a query parameter).
        _logger.LogInformation("Šaljem email za resetovanje lozinke korisniku {UserId}.", evt.UserId);

        var resetLink = $"{_frontendOptions.WebBaseUrl.TrimEnd('/')}/resetovanje-lozinke?token={Uri.EscapeDataString(evt.ResetToken)}";
        var message = new EmailMessageBuilder()
            .WithTo(evt.Email, evt.FirstName)
            .WithTemplate(EmailTemplate.PasswordReset, new PasswordResetData(evt.FirstName, resetLink))
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private async Task HandleAdminPasswordChangedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<AdminPasswordChangedNotification>(body, EventNames.AdminPasswordChanged);
        _logger.LogInformation("Šaljem obavještenje o promjeni lozinke korisniku {UserId}.", evt.UserId);

        var message = new EmailMessageBuilder()
            .WithTo(evt.Email, evt.FirstName)
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData(evt.FirstName))
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body, string routingKey)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body.Span)
                ?? throw new PoisonMessageException($"Prazan payload za event '{routingKey}'.");
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException($"Neispravan JSON za event '{routingKey}'.", ex);
        }
    }
}
