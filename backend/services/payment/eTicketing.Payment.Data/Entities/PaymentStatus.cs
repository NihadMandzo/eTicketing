namespace eTicketing.Payment.Data.Entities;

/// <summary>
/// Serialized as its integer ordinal (System.Text.Json's default, no converter on either side) and
/// deserialized straight back into eTicketing.Ticketing's separately-declared PaymentChargeStatus.
/// The two enums are therefore one wire format in two files: APPEND ONLY, never reorder, never
/// remove. PaymentEnumParityTests fails the build the moment they drift.
/// </summary>
public enum PaymentStatus
{
    Succeeded,
    Failed,

    /// <summary>An intent exists at the provider and the buyer has not finished paying yet, or has
    /// authorized but not been captured. Rows sit here between POST /purchases/payment-intent and
    /// POST /purchases.</summary>
    Pending,

    Refunded,

    /// <summary>The authorization was voided without ever being captured, so no money moved. This
    /// is the normal outcome when a buyer confirms just as their Redis hold expires.</summary>
    Cancelled,
}
