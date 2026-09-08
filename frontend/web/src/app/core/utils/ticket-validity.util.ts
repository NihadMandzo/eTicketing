import { Ticket } from '../models/purchase.models';

/**
 * Whether a ticket still gets its holder through the gate, and if not, why.
 *
 * A direct port of mobile's `ticket_validity.dart` — the two apps must never disagree about what a
 * given ticket's badge says, so the enum, the precedence rules and the Bosnian labels are all kept
 * in step deliberately.
 *
 * Two independent things decide this and both have to be checked: the ticket's own `TicketStatus`
 * (an organizer scanning it at the gate moves it to `Used`, terminal), and whether the thing it
 * admits you to has already happened. A ticket can be perfectly `Confirmed` and still worthless
 * because the concert was last month, and it can be scanned and spent on the morning of an event
 * that is still, by date, "upcoming". Neither signal alone answers the question a holder is actually
 * asking, which is "can I walk in with this".
 */
export type TicketValidity = 'valid' | 'pending' | 'used' | 'expired' | 'cancelled' | 'inside';

export const TICKET_VALIDITY_LABELS: Record<TicketValidity, string> = {
  valid: 'Važeća',
  /** Payment hasn't settled yet, so there is nothing to admit anyone with. Rare — purchases
   * complete on the critical path — but a `Processing` ticket must not claim to be valid. */
  pending: 'U obradi',
  /** Scanned at the gate. This is the state the whole list exists to make obvious. */
  used: 'Iskorištena',
  /** The event, day or subscription period is behind us. */
  expired: 'Istekla',
  cancelled: 'Otkazana',
  /**
   * RecurringReservation only, and the reason this enum has one more member than mobile's: a
   * subscription ticket is never spent by being used, it toggles in and out (see
   * TicketValidationService). "Unutra" is a valid ticket whose holder is currently inside — worth
   * distinguishing from plain "Važeća" because it is also the answer to "why won't the gate let me
   * in again": they have to scan out first.
   */
  inside: 'Unutra',
};

/** The one question worth asking. Everything else is the reason for the answer. */
export function isUsable(validity: TicketValidity): boolean {
  return validity === 'valid' || validity === 'inside';
}

/** A subscription ticket, told apart by the period columns only that mode populates — the same
 * test `TicketValidationService.IsMultiPassage` makes on the backend. */
export function isSubscriptionTicket(ticket: Ticket): boolean {
  return ticket.validFrom !== null && ticket.validTo !== null;
}

/**
 * Resolves a ticket's validity.
 *
 * `expiresAfter` is the last date the ticket is good for — `validTo` for a subscription period,
 * `validDate` for a day pass, the product's own date for a one-off event. Null means the date is
 * unknown (the product lookup failed), and an unknown date must not expire anything: a ticket
 * wrongly greyed out as expired is worse than one that leaves the question to the gate.
 *
 * Status wins over date. A cancelled or already-scanned ticket is finished regardless of when the
 * event is, and saying "Istekla" about a ticket somebody already used at the door would answer the
 * wrong question.
 */
export function resolveTicketValidity(
  ticket: Ticket,
  expiresAfter: Date | null,
  now: Date = new Date(),
): TicketValidity {
  switch (ticket.status) {
    case 'Cancelled':
      return 'cancelled';
    case 'Used':
      return 'used';
    case 'Processing':
      return 'pending';
  }

  if (expiresAfter !== null) {
    // Compared by calendar day, not instant: a ticket for tonight's show is still valid at 09:00
    // this morning, and one for a day pass is valid for the whole of its day.
    const lastValidDay = new Date(expiresAfter.getFullYear(), expiresAfter.getMonth(), expiresAfter.getDate());
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    if (lastValidDay < startOfToday) return 'expired';
  }

  // Checked last, and only for a ticket that is otherwise good: being inside says nothing useful
  // about a cancelled or lapsed one.
  return isSubscriptionTicket(ticket) && ticket.isInside ? 'inside' : 'valid';
}
