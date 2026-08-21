import { PublishStatus, TicketingMode } from './catalog.models';

export interface TicketType {
  id: string;
  sectorId: string;
  name: string;
  price: number;
  createdAt: string;
}

export interface Sector {
  id: string;
  productId: string;
  name: string;
  capacity: number;
  price: number;
  status: PublishStatus;
  ticketingMode: TicketingMode;
  periodYear: number | null;
  periodMonth: number | null;
  createdAt: string;
  ticketTypes: TicketType[];
}

export interface HoldSectorRequest {
  quantity: number;
  date?: string | null;
}

export interface HoldSectorResponse {
  holdId: string;
  expiresAt: string;
}
