using eTicketing.Contracts.Persistence;

namespace eTicketing.Contracts.Messaging;

/// <summary>
/// One inbound message a consumer has already processed, recorded in the same transaction as what
/// processing it changed.
///
/// <para><b>Why this exists.</b> The outbox makes delivery at-least-once, and RabbitMQ redelivers
/// anything unacked when a consumer's connection drops, so every consumer sees repeats. Most of what
/// the consumers do is safe to repeat anyway — a snapshot upsert, a status flip. Two things were
/// not: Ticketing's product-change and product-deletion fan-outs, which on a repeat wrote a second
/// set of per-buyer notifications under fresh ids that nothing downstream could recognise, and
/// Catalog's purchase signal, which counted the purchase twice.</para>
///
/// <para><b>Why a table rather than a cache.</b> Because the row commits atomically with the
/// consumer's own writes, "processed" and "recorded as processed" cannot disagree: there is no crash
/// window in which the effect landed but the record did not, or the other way round. That is the
/// property eTicketing.Notifications cannot have — its side effect is an email, which no database
/// transaction can take back — and the reason it uses Redis instead.</para>
///
/// <para><see cref="BaseEntity.CreatedAt"/> is when the message was processed; the cleanup job
/// deletes rows by it. Hard-deleted, per this repo's no-soft-delete rule.</para>
/// </summary>
public class InboxMessage : BaseEntity
{
    /// <summary>The AMQP message id. A string rather than a Guid because it comes off the wire: the
    /// platform's own publishers always send a Guid, but a message published by hand from the
    /// RabbitMQ management UI may carry anything.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Which consumer processed it — the queue name. Part of the key so that two consumers
    /// of the same event in one database would each process it once, rather than the first one
    /// silently claiming it for both.</summary>
    public string Consumer { get; set; } = string.Empty;
}
