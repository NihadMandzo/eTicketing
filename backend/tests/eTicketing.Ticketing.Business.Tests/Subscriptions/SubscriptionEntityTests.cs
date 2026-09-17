using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Subscriptions;

/// <summary>
/// The transition matrix, tested on the entity directly.
///
/// <para>These rules used to be loose property assignments spread across SubscriptionService and
/// three methods of SubscriptionRenewalService, which is how the same <c>NextRenewalAt</c> formula
/// ended up written twice and how a cancelled subscription could be walked back into Active. Now
/// there are four methods and this file is the specification of what each one refuses.</para>
///
/// <para>No fixture and no database: these are pure state transitions, and a Sqlite round trip would
/// only add a way for the test to fail that has nothing to do with the rule under test.</para>
/// </summary>
public class SubscriptionEntityTests
{
    private static readonly DateOnly PeriodStart = new(2026, 9, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 9, 30);
    private static readonly DateTime CancelledAt = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Subscription Active() =>
        Subscription.Create(
            Guid.NewGuid(), Guid.NewGuid(), "kupac@example.com",
            PeriodStart, PeriodEnd, "sub_test", "hold-1");

    // ── Create ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_StartsActiveWithTheProvidersOwnPeriod()
    {
        var subscription = Active();

        subscription.Id.Should().NotBeEmpty();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.CurrentPeriodStart.Should().Be(PeriodStart);
        subscription.CurrentPeriodEnd.Should().Be(PeriodEnd);
        subscription.CancelAtPeriodEnd.Should().BeFalse();
        subscription.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void Create_DerivesNextRenewalAsTheDayAfterThePeriodEnds()
    {
        var subscription = Active();

        subscription.NextRenewalAt.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Create_KeepsTheHoldIdWithoutWhichTheSpaceCanNeverBeReturned()
    {
        // CapacityHoldId is the only way to release a confirmed hold months later — ConfirmAsync
        // deletes the pointer ReleaseAsync would otherwise follow. A subscription created without it
        // leaves its parking bay sold forever.
        Active().CapacityHoldId.Should().Be("hold-1");
    }

    // ── Renew ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Renew_AdvancesThePeriodAndRecomputesTheNextRenewal()
    {
        var subscription = Active();

        var renewed = subscription.Renew(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        renewed.Should().BeTrue();
        subscription.CurrentPeriodStart.Should().Be(new DateOnly(2026, 10, 1));
        subscription.CurrentPeriodEnd.Should().Be(new DateOnly(2026, 10, 31));
        subscription.NextRenewalAt.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Renew_UsesTheSameNextRenewalRuleAsCreate()
    {
        // The one that used to be two copies of one expression, in two files. Same period in, same
        // answer out, whichever way the subscription got there.
        var created = Subscription.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, null);

        var renewed = Active();
        renewed.Renew(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        renewed.NextRenewalAt.Should().Be(created.NextRenewalAt);
    }

    [Fact]
    public void Renew_AfterAFailedMonth_ReturnsTheSubscriptionToActive()
    {
        // The provider got paid in the end, so PastDue is over.
        var subscription = Active();
        subscription.MarkPastDue();

        subscription.Renew(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Renew_OnACancelledSubscription_IsRefusedAndChangesNothing()
    {
        // The hole this guard closes. Cancelling releases the parking space back to the sector's
        // capacity, so a late subscription.renewed used to set Status back to Active and mint a
        // ticket for a bay that had already been resold — two people, one space, no complaint until
        // one of them finds the other parked in it.
        var subscription = Active();
        subscription.Cancel(CancelledAt);

        var renewed = subscription.Renew(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        renewed.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CurrentPeriodEnd.Should().Be(PeriodEnd);
        subscription.NextRenewalAt.Should().BeNull();
    }

    // ── MarkPastDue ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MarkPastDue_FromActive_MovesToPastDueAndKeepsTheSpace()
    {
        var subscription = Active();

        var marked = subscription.MarkPastDue();

        marked.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        // Deliberately untouched: the provider is still retrying and the buyer keeps the bay while
        // it does. Only an actual cancellation frees it.
        subscription.CapacityHoldId.Should().Be("hold-1");
        subscription.NextRenewalAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkPastDue_OnACancelledSubscription_IsRefused()
    {
        // A late failure notice must not resurrect a dead subscription into PastDue, where the
        // buyer's screen would show it as merely behind on payment rather than over.
        var subscription = Active();
        subscription.Cancel(CancelledAt);

        var marked = subscription.MarkPastDue();

        marked.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void MarkPastDue_Twice_LeavesItPastDue()
    {
        // Redeliveries are expected — eTicketing.Payment de-duplicates by provider event id, but
        // this side must not depend on that alone.
        var subscription = Active();
        subscription.MarkPastDue();

        subscription.MarkPastDue();

        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_EndsItAndStopsAdvertisingAFutureCharge()
    {
        var subscription = Active();

        var cancelled = subscription.Cancel(CancelledAt);

        cancelled.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelledAt.Should().Be(CancelledAt);
        // A dead subscription showing "obnavlja se 01.10." is a support ticket waiting to happen.
        subscription.NextRenewalAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_Twice_IsRefusedTheSecondTime()
    {
        // The caller depends on this answer for more than tidiness: it is the signal not to release
        // the sector's capacity again, which would hand the same space back to the counter twice and
        // let the sector oversell by one for every redelivered cancellation.
        var subscription = Active();
        subscription.Cancel(CancelledAt);

        var again = subscription.Cancel(CancelledAt.AddDays(1));

        again.Should().BeFalse();
        subscription.CancelledAt.Should().Be(CancelledAt);
    }

    [Fact]
    public void Cancel_FromPastDue_Works()
    {
        // The common real ending: the card kept failing and the provider gave up.
        var subscription = Active();
        subscription.MarkPastDue();

        var cancelled = subscription.Cancel(CancelledAt);

        cancelled.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    // ── ScheduleCancellation ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ScheduleCancellation_LeavesItActiveUntilTheProviderSaysOtherwise()
    {
        // The buyer paid for this month and keeps it. Flipping Status here would void a ticket they
        // are still entitled to use, and release a space they are still parking in.
        var subscription = Active();

        subscription.ScheduleCancellation();

        subscription.CancelAtPeriodEnd.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void ScheduleCancellation_OnAnAlreadyCancelledSubscription_DoesNothing()
    {
        var subscription = Active();
        subscription.Cancel(CancelledAt);

        subscription.ScheduleCancellation();

        subscription.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Fact]
    public void ScheduleCancellation_ThenCancel_RecordsTheRealEnding()
    {
        // The full buyer-initiated path: they ask, the month runs out, the provider's webhook
        // arrives.
        var subscription = Active();
        subscription.ScheduleCancellation();

        subscription.Cancel(CancelledAt);

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelAtPeriodEnd.Should().BeTrue();
        subscription.CancelledAt.Should().Be(CancelledAt);
    }
}
