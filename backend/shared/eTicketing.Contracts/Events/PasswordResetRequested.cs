namespace eTicketing.Contracts.Events;

public record PasswordResetRequested(
    Guid UserId,
    string Email,
    string FirstName,
    string ResetToken);
