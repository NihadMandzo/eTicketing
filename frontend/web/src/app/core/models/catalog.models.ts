export type TicketingMode = 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';

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
  status: 'Draft' | 'Published';
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
