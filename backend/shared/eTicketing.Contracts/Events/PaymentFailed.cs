namespace eTicketing.Contracts.Events;

public record PaymentFailed(
    int UserId,
    string UserEmail,
    string Reason,
    DateTime FailedAt);
