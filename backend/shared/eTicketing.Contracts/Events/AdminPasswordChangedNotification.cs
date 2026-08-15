namespace eTicketing.Contracts.Events;

public record AdminPasswordChangedNotification(
    Guid UserId,
    string Email,
    string FirstName);
