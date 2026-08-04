namespace eTicketing.Contracts.Events;

public record VerificationEmailRequested(
    int UserId,
    string Email,
    string FirstName,
    string VerificationCode);
