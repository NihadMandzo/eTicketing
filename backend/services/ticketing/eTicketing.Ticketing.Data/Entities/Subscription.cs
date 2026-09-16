using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// Tracks a RecurringReservation Sector's (e.g. one parking space) ongoing reservation by a buyer.
///
/// Renewal is not a job on this side: the payment provider charges the saved card on its own monthly
/// schedule and tells us afterwards, so the period below advances when a subscription.renewed event
/// arrives (see SubscriptionRenewalService), never on a timer here.
///
/// <para>Every setter is private and every state change goes through one of the four methods at the
/// bottom, the same shape <see cref="Ticket"/> already uses. The transitions used to live as loose
/// property assignments across two services — which is how <c>NextRenewalAt</c> ended up being
/// computed by the same expression in two different files, and how a cancelled subscription could be
/// walked back into <c>Active</c> by a late webhook.</para>
///
/// <para><b>Division of labour with the services:</b> the methods here guard and no-op, and never
/// construct a <c>Result</c> — dragging Contracts.Results semantics into <c>.Data</c> would make
/// every entity an HTTP-shaped thing. Choosing which <c>Error</c> a refusal maps to stays in
/// <see cref="eTicketing.Ticketing.Business"/>, which is also where the error codes the clients
/// depend on are written.</para>
/// </summary>
public class Subscription : BaseEntity
{
    public Guid Id { get; private set; }

    public Guid SectorId { get; private set; }

    // Navigation stays publicly settable: EF's fixup and the repository's Include both write it,
    // and it is a reference to another aggregate rather than part of this one's state.
    public Sector? Sector { get; set; }

    // Cross-service ref to Identity.User — plain Guid column, no FK.
    public Guid UserId { get; private set; }

    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Active;

    public DateOnly CurrentPeriodStart { get; private set; }
    public DateOnly CurrentPeriodEnd { get; private set; }

    /// <summary>When the next period is expected to begin — the day after the current one ends.
    /// Nothing on this side acts on it; it is what the buyer's "obnavlja se" line reads, and it is
    /// nulled the moment the subscription actually ends so a dead one never advertises a future
    /// charge. Derived rather than supplied, by <see cref="RenewalAfter"/>.</summary>
    public DateTime? NextRenewalAt { get; private set; }

    // Opaque provider subscription reference ("sub_..."), returned by eTicketing.Payment at first
    // purchase — never raw card data, keeps PCI-relevant surface inside Payment. Every renewal,
    // failure and cancellation webhook is keyed by this, hence the unique index in
    // SubscriptionConfiguration.
    public string? PaymentReference { get; private set; }

    /// <summary>
    /// The buyer's email, copied at purchase. A renewal mints its Ticket weeks later, from a webhook
    /// with no signed-in user anywhere in the request, so the address has to be on the subscription
    /// rather than looked up from the previous period's ticket.
    /// </summary>
    public string? UserEmail { get; private set; }

    /// <summary>
    /// Set when the buyer cancels. Status deliberately stays Active until the provider actually ends
    /// the subscription at period end: they paid for this month and their ticket stays valid for it.
    /// </summary>
    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>
    /// The Redis hold that was permanently confirmed for this space at first purchase.
    ///
    /// Needed because releasing a confirmed hold requires the hold id: the capacity counter is a hash
    /// keyed by hold id, and ConfirmAsync deletes the holdinfo pointer that would otherwise let
    /// ReleaseAsync find it. Without this column a cancelled parking subscription would leave its
    /// space sold forever. See ISectorCapacityLock.ReleaseConfirmedAsync.
    /// </summary>
    public string? CapacityHoldId { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    /// <summary>Private, not absent: EF Core materializes through it, and the factory below uses
    /// object-initializer syntax over it. Nothing outside this class can write these fields.</summary>
    private Subscription() { }

    /// <summary>
    /// The first period, minted alongside the first Ticket by PurchaseService.
    /// </summary>
    /// <param name="currentPeriodStart">The provider's own billing period, not a locally computed
    /// one — it is what the buyer's card statement will say.</param>
    /// <param name="capacityHoldId">The hold that was just confirmed for this space. Without it the
    /// space can never be handed back; see <see cref="CapacityHoldId"/>.</param>
    public static Subscription Create(
        Guid sectorId,
        Guid userId,
        string? userEmail,
        DateOnly currentPeriodStart,
        DateOnly currentPeriodEnd,
        string? paymentReference,
        string? capacityHoldId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            UserId = userId,
            UserEmail = userEmail,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = currentPeriodStart,
            CurrentPeriodEnd = currentPeriodEnd,
            NextRenewalAt = RenewalAfter(currentPeriodEnd),
            PaymentReference = paymentReference,
            CapacityHoldId = capacityHoldId,
        };

    /// <summary>
    /// Advances to the period the provider just charged for. Clears PastDue on the way: a renewal
    /// after a failed month means the provider got paid in the end.
    /// </summary>
    /// <returns>False if the subscription is already <see cref="SubscriptionStatus.Cancelled"/>, in
    /// which case nothing changed.
    ///
    /// <para>This guard is new, and it closes a real hole rather than tidying one. Cancelling
    /// releases the parking space back to the sector's capacity, so a late <c>subscription.renewed</c>
    /// arriving afterwards used to set Status back to Active and mint a ticket for a space that had
    /// already been resold — two people holding the same bay, with nothing anywhere saying so.</para>
    /// </returns>
    public bool Renew(DateOnly periodStart, DateOnly periodEnd)
    {
        if (Status == SubscriptionStatus.Cancelled)
            return false;

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        NextRenewalAt = RenewalAfter(periodEnd);
        Status = SubscriptionStatus.Active;
        return true;
    }

    /// <summary>
    /// The provider's renewal charge failed and it is working through its own dunning retries. The
    /// space is deliberately NOT released — the buyer keeps it while the provider keeps trying, and
    /// only an actual cancellation frees it.
    /// </summary>
    /// <returns>False if already cancelled: a late failure notice must not resurrect a dead
    /// subscription into PastDue.</returns>
    public bool MarkPastDue()
    {
        if (Status == SubscriptionStatus.Cancelled)
            return false;

        Status = SubscriptionStatus.PastDue;
        return true;
    }

    /// <summary>
    /// The subscription has actually ended. Terminal.
    /// </summary>
    /// <returns>False if it was already cancelled — which the caller needs, because it is also the
    /// signal not to release the sector's capacity a second time.</returns>
    public bool Cancel(DateTime at)
    {
        if (Status == SubscriptionStatus.Cancelled)
            return false;

        Status = SubscriptionStatus.Cancelled;
        CancelledAt = at;
        // Nothing will renew it, so it must stop advertising a next charge.
        NextRenewalAt = null;
        return true;
    }

    /// <summary>
    /// The buyer asked to stop, and the provider agreed to end it when the paid-for period runs out.
    /// Status stays Active on purpose: they paid for this month and their ticket is valid until it
    /// ends. Only <see cref="Cancel"/>, driven by the provider's webhook, ends it for real.
    /// </summary>
    public void ScheduleCancellation()
    {
        if (Status == SubscriptionStatus.Cancelled)
            return;

        CancelAtPeriodEnd = true;
    }

    /// <summary>The one definition of when the next period starts. It lived as the same expression
    /// in PurchaseService and SubscriptionRenewalService before this — two copies of a rule that has
    /// to agree, which is the sort of thing that only stops agreeing once.</summary>
    private static DateTime RenewalAfter(DateOnly periodEnd) =>
        periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
}
