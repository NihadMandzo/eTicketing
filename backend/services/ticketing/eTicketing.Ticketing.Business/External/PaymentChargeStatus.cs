namespace eTicketing.Ticketing.Business.External;

/// <summary>
/// Mirrors eTicketing.Payment.Data.Entities.PaymentStatus member-for-member, in order. Both sides
/// are serialized as the integer ordinal (System.Text.Json's default, no converter on either side),
/// so these two separately-declared enums are really one wire format living in two files: APPEND
/// ONLY, never reorder, never remove. PaymentEnumParityTests fails the build if they drift.
/// </summary>
public enum PaymentChargeStatus
{
    Succeeded,
    Failed,
    Pending,
    Refunded,
    Cancelled,
}
