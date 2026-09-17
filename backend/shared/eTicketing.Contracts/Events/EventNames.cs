namespace eTicketing.Contracts.Events;

public static class EventNames
{
    public const string Exchange = "eticketing.events";

    public const string TicketPurchased = "ticket.purchased";
    public const string VerificationEmailRequested = "verification-email.requested";
    public const string PaymentFailed = "payment.failed";
    public const string OrganizationCreated = "organization.created";

    /// <summary>eTicketing.Identity → eTicketing.Ticketing. The organization's contact details after
    /// any change to them, feeding the OrganizationSnapshot read model that replaced the
    /// Ticketing→Identity HTTP call. Note this is a different thing from
    /// <see cref="OrganizationCreated"/>, which is an email to the founding admin.</summary>
    public const string OrganizationSnapshotChanged = "organization.changed";

    /// <summary>eTicketing.Identity → eTicketing.Ticketing. Drop the organization's snapshot row.</summary>
    public const string OrganizationDeleted = "organization.deleted";
    public const string OrganizationAdminDeleted = "organization-admin.deleted";
    public const string PasswordResetRequested = "password-reset.requested";
    public const string AdminPasswordChanged = "admin-password.changed";

    /// <summary>eTicketing.PdfGeneration → eTicketing.Notifications (send the confirmation email
    /// with every ticket PDF attached) + eTicketing.Ticketing (flip Ticket.Status Confirmed →
    /// Ready). One event per order, not per ticket.</summary>
    public const string TicketPdfReady = "ticket-pdf.ready";

    /// <summary>eTicketing.Catalog → eTicketing.Ticketing. Catalog owns Product but has no idea
    /// who bought a ticket for it; Ticketing does, so it's the one that fans this out into
    /// per-buyer <see cref="ProductChanged"/> notifications.</summary>
    public const string ProductUpdated = "product.updated";

    /// <summary>eTicketing.Ticketing → eTicketing.Notifications. One event per distinct buyer
    /// email, already resolved — Notifications never has to ask anyone who the buyers are.</summary>
    public const string ProductChanged = "product-change.notification";

    /// <summary>eTicketing.Catalog → eTicketing.Ticketing. Same hop as <see cref="ProductUpdated"/>
    /// and for the same reason — Catalog deletes the product but has no idea who bought a ticket
    /// for it. Also hard-deletes Ticketing's ProductSnapshot row, which is what stops a deleted
    /// product's sectors from going on selling forever.</summary>
    public const string ProductDeleted = "product.deleted";

    /// <summary>eTicketing.Catalog → eTicketing.Ticketing. The product's current state after any
    /// change to it, feeding the ProductSnapshot read model. Distinct from
    /// <see cref="ProductUpdated"/> on purpose — see
    /// <see cref="Events.ProductSnapshotChanged"/> for why one cannot serve both jobs.</summary>
    public const string ProductSnapshotChanged = "product.changed";

    /// <summary>eTicketing.Ticketing → eTicketing.Notifications. One event per recipient
    /// (each affected buyer, plus the organization itself when platform staff did the deleting),
    /// address already resolved.</summary>
    public const string ProductDeletedNotification = "product-deleted.notification";

    /// <summary>eTicketing.Payment → eTicketing.Ticketing. A recurring period was paid for; mint
    /// that period's ticket. See <see cref="Events.SubscriptionRenewed"/> for why this is an event
    /// rather than a synchronous call back into Ticketing.</summary>
    public const string SubscriptionRenewed = "subscription.renewed";

    /// <summary>eTicketing.Payment → eTicketing.Ticketing. A renewal charge failed; the
    /// subscription goes PastDue while the provider keeps retrying.</summary>
    public const string SubscriptionPaymentFailed = "subscription.payment-failed";

    /// <summary>eTicketing.Payment → eTicketing.Ticketing. The subscription ended for good; release
    /// the space back to its sector's capacity.</summary>
    public const string SubscriptionCancelled = "subscription.cancelled";
}
