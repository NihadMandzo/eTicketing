using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// One row per admission unit (one QR code / receipt line), minted by PurchaseService for an online
/// sale or by TicketPrintService for a printed batch. A single POST /purchases call can mint
/// several Tickets at once (e.g. 2x Odrasli + 1x Djeca) — they all share OrderId so "my tickets"
/// and receipt UIs can group them back into one order.
/// </summary>
public class Ticket : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    // Null iff Sector.TicketTypes was empty at purchase time (the single-implicit-price path).
    public Guid? TicketTypeId { get; private set; }
    public TicketType? TicketType { get; set; }

    // Groups every Ticket row minted by one POST /purchases call — also reused as the OrderRef
    // sent to eTicketing.Payment, so a Payment row and its Tickets share the same identifier. For
    // printed tickets this carries the print batch id, so the same grouping keeps working.
    public Guid OrderId { get; set; }

    // Denormalized from Sector.ProductId — avoids a join for common "my tickets" filters and
    // matches the shape of the TicketPurchased integration event.
    public Guid ProductId { get; set; }

    // Cross-service ref to Identity.User — plain Guid column, no FK. NULL when Origin is Printed:
    // a ticket sold over a box-office counter has no account behind it.
    public Guid? UserId { get; private set; }

    // Denormalized at purchase time for email/PDF even if the user record later changes. NULL when
    // Origin is Printed, for the same reason as UserId.
    public string? UserEmail { get; private set; }

    public TicketStatus Status { get; set; } = TicketStatus.Processing;
    public decimal PricePaid { get; set; }

    // How this ticket was issued. Set once by the factories below and never changed afterwards.
    public TicketOrigin Origin { get; private set; } = TicketOrigin.Online;

    // Printed tickets only: which export batch minted this row, and the human-readable running
    // number printed on the stub ("#000482"). Buyers never see either — an online ticket is
    // identified by its GUID alone.
    public Guid? PrintBatchId { get; private set; }
    public TicketPrintBatch? PrintBatch { get; set; }
    public int? SerialNumber { get; private set; }

    // DailyEntry only: which specific calendar date (within Sector.PeriodYear/PeriodMonth) this
    // ticket admits entry for.
    public DateOnly? ValidDate { get; private set; }

    // RecurringReservation only: which billing period this specific ticket instance covers. The
    // first manual purchase creates the Subscription + this first Ticket; each later auto-renewal
    // mints one more Ticket linked to the same Subscription.
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public Subscription? Subscription { get; set; }

    // Set exactly once, by MarkValidated, when an organizer scans this ticket at the gate. Private
    // setters for the same reason ValidDate/ValidFrom/ValidTo have them: the only way to write
    // either is through the one method that also flips Status to Used, so a validated ticket can
    // never exist with a timestamp but a still-admittable status (or vice versa).
    public DateTime? ValidatedAt { get; private set; }
    public Guid? ValidatedByUserId { get; private set; }

    // Set alongside the two above when the scan came from an unattended gate scanner rather than a
    // person holding a phone (see GateDevice). ValidatedByUserId still names the organizer who
    // registered that device, so "who admitted this" always resolves to an accountable human; this
    // column additionally says which door they were admitted through.
    public Guid? ValidatedByDeviceId { get; private set; }

    // ── RecurringReservation admission state ────────────────────────────────────────────────────
    //
    // A monthly parking space is not a one-shot admission: the holder drives in and out all month
    // on the same ticket, so MarkValidated's terminal Status=Used would lock them out after their
    // very first entry. These three columns carry the alternative — an entry/exit toggle over the
    // ticket's ValidFrom..ValidTo window — and are only ever written for RecurringReservation
    // tickets. Every other mode leaves them at their defaults and keeps burning on first scan.

    /// <summary>True between a recorded entry and the matching exit. The invariant the whole
    /// feature exists for: an entry scan is only admitted while this is false, so the same ticket
    /// can never be used to enter twice without an exit in between.</summary>
    public bool IsInside { get; private set; }

    public DateTime? LastEntryAt { get; private set; }
    public DateTime? LastExitAt { get; private set; }

    /// <summary>How many times this ticket has been admitted over its whole period. Never reset —
    /// it is the audit trail for a ticket that is deliberately not consumed by being used.</summary>
    public int EntryCount { get; private set; }

    // Parameterless constructor stays available (private, not public) for EF Core materialization
    // and the object-initializer syntax the factories below use — nothing outside this class can
    // call `new Ticket { ... }` any more, so ValidDate/ValidFrom/ValidTo/SubscriptionId can only
    // ever be set through the mode-specific combination a factory below encodes.
    private Ticket() { }

    /// <summary>TicketingMode.SingleOccurrence: no ValidDate/ValidFrom/ValidTo/SubscriptionId.</summary>
    public static Ticket ForSingleOccurrence(
        Guid sectorId, Guid? ticketTypeId, Guid orderId, Guid productId, Guid userId, string userEmail, decimal pricePaid) =>
        new()
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            TicketTypeId = ticketTypeId,
            OrderId = orderId,
            ProductId = productId,
            UserId = userId,
            UserEmail = userEmail,
            Status = TicketStatus.Confirmed,
            PricePaid = pricePaid,
        };

    /// <summary>TicketingMode.DailyEntry: carries ValidDate, no ValidFrom/ValidTo/SubscriptionId.</summary>
    public static Ticket ForDailyEntry(
        Guid sectorId, Guid? ticketTypeId, Guid orderId, Guid productId, Guid userId, string userEmail, decimal pricePaid, DateOnly? validDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            TicketTypeId = ticketTypeId,
            OrderId = orderId,
            ProductId = productId,
            UserId = userId,
            UserEmail = userEmail,
            Status = TicketStatus.Confirmed,
            PricePaid = pricePaid,
            ValidDate = validDate,
        };

    /// <summary>TicketingMode.RecurringReservation: carries ValidFrom/ValidTo/SubscriptionId, no
    /// ValidDate.</summary>
    public static Ticket ForRecurringReservation(
        Guid sectorId, Guid? ticketTypeId, Guid orderId, Guid productId, Guid userId, string userEmail, decimal pricePaid,
        Guid subscriptionId, DateOnly validFrom, DateOnly validTo) =>
        new()
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            TicketTypeId = ticketTypeId,
            OrderId = orderId,
            ProductId = productId,
            UserId = userId,
            UserEmail = userEmail,
            Status = TicketStatus.Confirmed,
            PricePaid = pricePaid,
            SubscriptionId = subscriptionId,
            ValidFrom = validFrom,
            ValidTo = validTo,
        };

    /// <summary>A physical ticket minted for printing: no buyer, a running SerialNumber for the
    /// printed stub, and the batch id doubling as OrderId so existing per-order grouping keeps
    /// working. <paramref name="validDate"/> is set for a DailyEntry batch and null for a
    /// SingleOccurrence one — the two modes the export supports (see TicketPrintService). Starts
    /// life Confirmed, exactly like a paid ticket, because the paper is the proof of sale and the
    /// gate must admit it.</summary>
    public static Ticket ForPrint(
        Guid sectorId, Guid? ticketTypeId, Guid batchId, Guid productId, decimal price, int serialNumber, DateOnly? validDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            TicketTypeId = ticketTypeId,
            OrderId = batchId,
            ProductId = productId,
            UserId = null,
            UserEmail = null,
            Status = TicketStatus.Confirmed,
            PricePaid = price,
            Origin = TicketOrigin.Printed,
            PrintBatchId = batchId,
            SerialNumber = serialNumber,
            ValidDate = validDate,
        };

    /// <summary>Admits this ticket at the gate: records who scanned it and when, and moves it to
    /// the terminal <see cref="TicketStatus.Used"/> so a second scan of the same QR is rejected.
    /// Callers must already hold the per-ticket Redis validation lock and must already have
    /// checked the ticket is currently admittable — this method does not re-check, it commits.
    ///
    /// SingleOccurrence and DailyEntry only. A RecurringReservation ticket must go through
    /// <see cref="RegisterEntry"/>/<see cref="RegisterExit"/> instead — burning it would end a
    /// month-long parking subscription on its first morning.</summary>
    public void MarkValidated(Guid byUserId, DateTime at, Guid? byDeviceId = null)
    {
        Status = TicketStatus.Used;
        ValidatedAt = at;
        ValidatedByUserId = byUserId;
        ValidatedByDeviceId = byDeviceId;
    }

    /// <summary>Admits a RecurringReservation ticket through the gate without consuming it: Status
    /// stays <see cref="TicketStatus.Confirmed"/> for the rest of the period and only
    /// <see cref="IsInside"/> flips. Same preconditions as <see cref="MarkValidated"/> — the caller
    /// holds the validation lock and has already decided this scan is admissible.</summary>
    public void RegisterEntry(Guid byUserId, DateTime at, Guid? byDeviceId = null)
    {
        IsInside = true;
        LastEntryAt = at;
        EntryCount++;
        ValidatedAt = at;
        ValidatedByUserId = byUserId;
        ValidatedByDeviceId = byDeviceId;
    }

    /// <summary>Records the holder leaving, which is what re-arms the ticket for its next entry.
    /// ValidatedAt/ValidatedByUserId are updated too so "last seen at this gate" stays truthful —
    /// an exit is as much a scan as an entry.</summary>
    public void RegisterExit(Guid byUserId, DateTime at, Guid? byDeviceId = null)
    {
        IsInside = false;
        LastExitAt = at;
        ValidatedAt = at;
        ValidatedByUserId = byUserId;
        ValidatedByDeviceId = byDeviceId;
    }
}
