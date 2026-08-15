using eTicketing.Notifications.Email;

namespace eTicketing.Notifications.Sending;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
