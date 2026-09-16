namespace eTicketing.Notifications.Email.Templates;

public sealed record OrganizationAdminDeletedData(string OrganizationName, string DeletedAdminFullName, string Reason);
