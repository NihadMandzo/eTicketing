using eTicketing.Contracts.Persistence;

namespace eTicketing.Contracts.Messaging;

/// <summary>
/// One event waiting to be published, written in the same transaction as the data that caused it.
///
/// <para><b>Why this exists.</b> Every write-path publish in this platform used to happen *after*
/// SaveChangesAsync and swallow its own exceptions. That is two failure modes at once: the process
/// can die between the commit and the publish, and a broker that is simply down loses the event
/// with nothing but a log line. While events only triggered side effects the cost was a missed
/// email. Once they became the data-sync channel between services, the same gap silently desyncs a
/// read model — and nothing would ever notice.</para>
///
/// <para>Writing the row inside the caller's own transaction makes the event and the data it
/// describes atomic: either both land or neither does. <see cref="Messaging"/>'s dispatcher then
/// moves rows to the broker at its own pace and deletes them once the broker has confirmed.</para>
///
/// <para><b>Delivery becomes at-least-once.</b> A dispatcher that publishes, is confirmed, and dies
/// before deleting the row will publish again on restart, under the same <see cref="Id"/>.
/// eTicketing.Notifications recognises that repeat by the id (DeduplicatingDeliveryHandler), so a
/// redelivery does not re-send the email. The other consumers have no general inbox. Most of what they
/// do is safe to repeat anyway (snapshot upserts, status flips, and a renewal that checks whether its
/// period was already minted), but two known gaps remain: Catalog's recommendation signal counts a
/// redelivered purchase twice, and Ticketing's product-change and product-deletion fan-outs, if
/// redelivered, write fresh rows under new ids that Notifications cannot recognise as repeats.</para>
/// </summary>
public class OutboxMessage : BaseEntity
{
    /// <summary>Also the AMQP message id — the key a consumer dedupes a repeated publish on.</summary>
    public Guid Id { get; set; }

    /// <summary>The topic-exchange routing key — an EventNames constant.</summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>The event, already serialized. Stored as JSON text rather than as a typed column
    /// so this table never needs to know the event shapes, and an old row still dispatches after
    /// the publisher's type has moved on.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>How many times the dispatcher has tried and failed. Kept to make a stuck row
    /// visible — a payload the broker keeps rejecting would otherwise be retried silently for
    /// ever, indistinguishable from an idle queue.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Why the last attempt failed, truncated. Null until something goes wrong.</summary>
    public string? LastError { get; set; }
}
