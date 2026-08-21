import { coerceEnum } from '../utils/api-enum.util';

export type TicketingMode = 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';
export type PublishStatus = 'Draft' | 'Published';

// Declaration order must match eTicketing.Contracts.Persistence.TicketingMode
// and .PublishStatus exactly — the wire value is the ordinal, see coerceEnum.
const TICKETING_MODES: readonly TicketingMode[] = ['SingleOccurrence', 'DailyEntry', 'RecurringReservation'];
const PUBLISH_STATUSES: readonly PublishStatus[] = ['Draft', 'Published'];

export const toTicketingMode = (raw: unknown): TicketingMode => coerceEnum(raw, TICKETING_MODES, 'SingleOccurrence');
export const toPublishStatus = (raw: unknown): PublishStatus => coerceEnum(raw, PUBLISH_STATUSES, 'Published');

export interface Category {
  id: number;
  name: string;
  ticketingMode: TicketingMode;
  iconUrl: string | null;
}

export interface ProductImage {
  id: string;
  url: string;
  displayOrder: number;
}

export interface Product {
  id: string;
  name: string;
  description: string;
  date: string | null;
  categoryId: number;
  categoryName: string;
  ticketingMode: TicketingMode;
  organizationId: string;
  status: PublishStatus;
  images: ProductImage[];
  createdAt: string;
}

export interface ProductQuery {
  page?: number;
  pageSize?: number;
  fts?: string | null;
  categoryId?: number | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
