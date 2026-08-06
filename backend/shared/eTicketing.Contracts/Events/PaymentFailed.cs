namespace eTicketing.Contracts.Events;

public record PaymentFailed(
    Guid UserId,
    string UserEmail,
    string Reason,
    DateTime FailedAt);
