namespace eTicketing.Contracts.Events;

public record VerificationEmailRequested(
    Guid UserId,
    string Email,
    string FirstName,
    string VerificationCode);
