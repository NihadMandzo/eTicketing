import { coerceEnum } from '../utils/api-enum.util';

export type TicketStatus = 'Processing' | 'Confirmed' | 'Ready' | 'Cancelled';

// Declaration order must match eTicketing.Ticketing.Data.Entities.TicketStatus
// exactly — the wire value is the ordinal, see coerceEnum.
const TICKET_STATUSES: readonly TicketStatus[] = ['Processing', 'Confirmed', 'Ready', 'Cancelled'];

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
}

export interface PurchaseResponse {
  orderId: string;
  productId: string;
  sectorId: string;
  totalPaid: number;
  purchasedAt: string;
  tickets: Ticket[];
}
