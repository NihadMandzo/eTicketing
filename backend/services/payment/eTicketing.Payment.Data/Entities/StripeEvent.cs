using eTicketing.Contracts.Persistence;

namespace eTicketing.Payment.Data.Entities;

/// <summary>
/// One row per webhook event this service has already processed, keyed by Stripe's own event id.
///
/// Stripe guarantees at-least-once delivery and retries anything that does not answer 2xx, so the
/// same "invoice.paid" can and does arrive more than once. Without this table a redelivered
/// renewal would mint a second parking ticket for a month that was only paid for once. The id is
/// the primary key rather than a surrogate with a unique index, so a duplicate insert fails at the
/// database rather than needing a read-then-write.
///
/// Scope, stated exactly: a row is written only AFTER its event has been published, so this table
/// stops *sequential* redelivery -- the retry-minutes-later case that is essentially all of them.
/// It does not by itself stop two deliveries of the same event that are in flight simultaneously;
/// that window is covered downstream by Ticketing's per-period ticket guard. StripeWebhookService's
/// class comment explains why the ordering is deliberately that way round.
/// </summary>
public class StripeEvent : BaseEntity
{
    /// <summary>Stripe's "evt_..." identifier.</summary>
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    // When it was processed is exactly BaseEntity.CreatedAt, set by
    // AuditableEntitySaveChangesInterceptor -- a separate ProcessedAt column would always hold the
    // same value.
}
