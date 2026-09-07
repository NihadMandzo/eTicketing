import { coerceEnum } from '../utils/api-enum.util';

export type TicketStatus = 'Processing' | 'Confirmed' | 'Ready' | 'Cancelled' | 'Used';

// Declaration order must match eTicketing.Ticketing.Data.Entities.TicketStatus
// exactly — the wire value is the ordinal, see coerceEnum. 'Used' is appended
// (never inserted) for the same reason: an organizer scanning a ticket at the
// gate moves it to that terminal status.
const TICKET_STATUSES: readonly TicketStatus[] = [
  'Processing',
  'Confirmed',
  'Ready',
  'Cancelled',
  'Used',
];

export const toTicketStatus = (raw: unknown): TicketStatus => coerceEnum(raw, TICKET_STATUSES, 'Confirmed');

export interface PurchaseLineItem {
  ticketTypeId?: string | null;
  quantity: number;
}

/** Which payment gateway the backend is configured with. Returned on every payment-intent
 * response rather than compiled in, so switching providers needs no rebuild of this app. */
export type PaymentProvider = 'Mock' | 'Stripe';

/** Prices the hold server-side and creates the payment object the buyer confirms. */
export interface CreatePaymentIntentRequest {
  holdId: string;
  lineItems: PurchaseLineItem[];
}

export interface PaymentIntentResponse {
  provider: PaymentProvider;
  /** Null in Mock mode — there is no payment SDK to initialise. */
  publishableKey: string | null;
  orderId: string;
  intentId: string;
  clientSecret: string | null;
  /** Authoritative, computed by the backend from the held sector. Never sent by this app. */
  amount: number;
  currency: string;
  isSubscription: boolean;
}

/**
 * Completes a purchase the buyer has already paid for. Carries no card data: with the Stripe
 * provider the card goes straight from this browser to Stripe and only identifiers come back.
 */
export interface PurchaseRequest {
  holdId: string;
  lineItems: PurchaseLineItem[];
  orderId: string;
  paymentIntentId: string;
  /** Mock provider only — "0000" simulates a decline. Omitted entirely in Stripe mode. */
  simulatedLast4?: string | null;
}

export interface Ticket {
  id: string;
  orderId: string;
  sectorId: string;
  sectorName: string;
  productId: string;
  ticketTypeId: string | null;
  ticketTypeName: string | null;
  status: TicketStatus;
  pricePaid: number;
  validDate: string | null;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
  /** The signed code this ticket's QR encodes — what a gate scanner reads back. */
  qrPayload: string;
  /**
   * Ready-to-render `data:image/png;base64,...` QR, produced server-side by
   * eTicketing.Ticketing. Rendered there rather than here so web and mobile
   * share one QR implementation and neither needs a QR library of its own.
   */
  qrImage: string;
}

export interface PurchaseResponse {
  orderId: string;
  productId: string;
  sectorId: string;
  totalPaid: number;
  purchasedAt: string;
  tickets: Ticket[];
}
