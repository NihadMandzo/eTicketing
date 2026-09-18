namespace eTicketing.Contracts.Events;

/// <summary>Which of the two audiences a <see cref="ProductDeletedNotification"/> is written
/// for. The wording differs materially — a buyer is told their ticket is void and who to chase
/// for their money; an organizer is told the platform removed their product.</summary>
public enum ProductDeletedAudience
{
    Buyer,
    Organizer
}
