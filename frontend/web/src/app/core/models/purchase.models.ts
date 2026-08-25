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

export interface PurchaseRequest {
  holdId: string;
  lineItems: PurchaseLineItem[];
  cardNumber: string;
  cardExpiry: string;
  cardCvv: string;
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
