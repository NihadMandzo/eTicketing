using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Subscriptions;

/// <summary>
/// The webhook-driven half of subscriptions: what happens to a parking reservation months after
/// anyone was last on the site.
///
/// Every method here is keyed by the provider's subscription reference and is safe to run twice --
/// deliveries are at-least-once, and although eTicketing.Payment de-duplicates by provider event id,
/// this side must not depend on that alone.
/// </summary>
public interface ISubscriptionRenewalService
{
    Task RenewAsync(SubscriptionRenewed message, CancellationToken ct = default);

    Task MarkPastDueAsync(SubscriptionPaymentFailed message, CancellationToken ct = default);

    Task CancelAsync(SubscriptionCancelled message, CancellationToken ct = default);
}
