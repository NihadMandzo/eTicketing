namespace eTicketing.Contracts.Events;

public record OrganizationCreatedNotification(
    Guid OrganizationId,
    string OrganizationName,
    string RecipientEmail);
