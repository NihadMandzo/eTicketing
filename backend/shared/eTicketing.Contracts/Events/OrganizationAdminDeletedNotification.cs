namespace eTicketing.Contracts.Events;

public record OrganizationAdminDeletedNotification(
    Guid OrganizationId,
    string OrganizationName,
    string DeletedAdminFullName,
    string RecipientEmail,
    string Reason);
