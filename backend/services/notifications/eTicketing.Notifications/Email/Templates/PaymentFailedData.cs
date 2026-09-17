namespace eTicketing.Notifications.Email.Templates;

/// <param name="ReasonCode">The payment provider's raw failure code. The template decides what, if
/// anything, to say about it — see <see cref="PaymentFailedTemplate.DescribeReason"/>.</param>
public sealed record PaymentFailedData(
    Guid? OrderId,
    string? SectorName,
    decimal? Amount,
    string ReasonCode,
    DateTime FailedAt);
