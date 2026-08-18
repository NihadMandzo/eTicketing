namespace eTicketing.Contracts.Persistence;

/// <summary>
/// Governs how a <c>Product</c>/<c>Sector</c>/<c>Ticket</c> behave, set once per
/// <c>Category</c> so new business categories can reuse an existing mode with zero
/// code changes. See .claude/rules/01-domain.md for the full description of each mode.
/// </summary>
public enum TicketingMode
{
    /// <summary>Classic one-time event: Product.Date is the single showing date/time,
    /// Sector is a seating/capacity section (e.g. VIP/Regular) with a fixed Capacity.</summary>
    SingleOccurrence,

    /// <summary>Museum-style day pass: Sector defines one flat Capacity+Price per calendar
    /// month (PeriodYear/PeriodMonth); the buyer picks an exact date at purchase time and
    /// capacity is tracked per (Sector, date).</summary>
    DailyEntry,

    /// <summary>Monthly parking-style space subscription: Sector represents one specific
    /// labeled space (Capacity always 1); renewal is tracked via a Subscription.</summary>
    RecurringReservation
}
