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
  status: 'Processing' | 'Confirmed' | 'Ready' | 'Cancelled';
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
