namespace eTicketing.Contracts.Events;

public static class EventNames
{
    public const string Exchange = "eticketing.events";

    public const string TicketPurchased = "ticket.purchased";
    
    public const string VerificationEmailRequested = "verification-email.requested";

    public const string PaymentFailed = "payment.failed";

    public const string OrganizationCreated = "organization.created";
    public const string OrganizationSnapshotChanged = "organization.changed";

    public const string OrganizationDeleted = "organization.deleted";
    public const string OrganizationAdminDeleted = "organization-admin.deleted";
    public const string PasswordResetRequested = "password-reset.requested";
    public const string AdminPasswordChanged = "admin-password.changed";

    public const string TicketPdfReady = "ticket-pdf.ready";

    public const string ProductUpdated = "product.updated";
    public const string ProductChanged = "product-change.notification";
    public const string ProductDeleted = "product.deleted";
    public const string ProductSnapshotChanged = "product.changed";
    public const string ProductDeletedNotification = "product-deleted.notification";

    public const string SubscriptionRenewed = "subscription.renewed";
    public const string SubscriptionPaymentFailed = "subscription.payment-failed";
    public const string SubscriptionCancelled = "subscription.cancelled";
}
