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
    /// <summary>Brevo caps a single request's attachments; staying comfortably under it matters
    /// more than delivering every PDF of an implausibly large order, because busting the cap
    /// fails the whole email rather than one attachment. At ~50 KB per ticket this is thousands of
    /// tickets, so in practice it never trips — it exists so that if it ever does, the buyer still
    /// gets an email listing every ticket code instead of nothing at all.</summary>
    private const int MaxTotalAttachmentBytes = 9 * 1024 * 1024;

    private readonly IEmailSender _emailSender;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEmailSender emailSender,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<NotificationDispatcher> logger)
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
        EventNames.TicketPdfReady => HandleTicketPdfReadyAsync(body, ct),
        EventNames.ProductChanged => HandleProductChangedAsync(body, ct),
        EventNames.ProductDeletedNotification => HandleProductDeletedAsync(body, ct),
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

    /// <summary>The purchase confirmation. Deliberately triggered by ticket-pdf.ready rather than
    /// ticket.purchased: the PDFs are the point of the email, so it can only be sent once they
    /// exist. eTicketing.PdfGeneration's own retry ladder guarantees this event eventually
    /// arrives.</summary>
    private async Task HandleTicketPdfReadyAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<TicketPdfReady>(body, EventNames.TicketPdfReady);
        _logger.LogInformation("Šaljem potvrdu kupovine za narudžbu {OrderId}.", evt.OrderId);

        var data = new TicketsReadyData(
            evt.ProductName,
            evt.ProductDate?.ToString("dd.MM.yyyy. HH:mm") ?? "Datum po ulaznici",
            evt.ProductCity,
            evt.TotalPaid,
            evt.Tickets.Select(t => new TicketsReadyLine(
                // Short, readable code — the same 8-character prefix the PDF filename uses, so the
                // row and the attachment it refers to are visibly the same ticket.
                t.TicketId.ToString("N")[..8].ToUpperInvariant(),
                t.SectorName, t.TicketTypeName, t.PricePaid)).ToList());

        var builder = new EmailMessageBuilder()
            .WithTo(evt.UserEmail)
            .WithTemplate(EmailTemplate.TicketsReady, data);

        var totalBytes = 0;
        foreach (var ticket in evt.Tickets)
        {
            if (totalBytes + ticket.Content.Length > MaxTotalAttachmentBytes)
            {
                _logger.LogWarning(
                    "Prilozi za narudžbu {OrderId} prelaze dozvoljenu veličinu — preostale ulaznice nisu priložene.",
                    evt.OrderId);
                break;
            }

            totalBytes += ticket.Content.Length;
            builder.WithAttachment(ticket.FileName, ticket.Content);
        }

        await _emailSender.SendAsync(builder.Build(), ct);
    }

    /// <summary>Already fanned out per-recipient by eTicketing.Ticketing (which is the only service
    /// that knows who the buyers are) — one event in, one email out, no lookup here.</summary>
    private async Task HandleProductChangedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<ProductChangedNotification>(body, EventNames.ProductChanged);
        _logger.LogInformation("Šaljem obavještenje o izmjeni proizvoda {ProductId}.", evt.ProductId);

        var data = new ProductChangedData(
            evt.ProductName,
            evt.Changes.Select(c => new ProductChangeLine(c.Field, c.OldValue, c.NewValue)).ToList());

        var message = new EmailMessageBuilder()
            .WithTo(evt.RecipientEmail)
            .WithTemplate(EmailTemplate.ProductChanged, data)
            .Build();

        await _emailSender.SendAsync(message, ct);
    }

    private async Task HandleProductDeletedAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var evt = Deserialize<ProductDeletedNotification>(body, EventNames.ProductDeletedNotification);
        _logger.LogInformation(
            "Šaljem obavještenje o brisanju proizvoda {ProductId} ({Audience}).", evt.ProductId, evt.Audience);

        var data = new ProductDeletedData(
            evt.ProductName,
            evt.Audience,
            evt.ProductDate,
            evt.OrganizerName,
            evt.OrganizerEmail,
            evt.OrganizerPhone,
            evt.TicketCount);

        var message = new EmailMessageBuilder()
            .WithTo(evt.RecipientEmail)
            .WithTemplate(EmailTemplate.ProductDeleted, data)
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
