import { coerceEnum } from '../utils/api-enum.util';

export type TicketingMode = 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';
export type PublishStatus = 'Draft' | 'Published';
export type City = 'Sarajevo' | 'Mostar' | 'BanjaLuka' | 'Tuzla' | 'Zenica' | 'Bihac' | 'Brcko' | 'Trebinje';

// Declaration order must match eTicketing.Contracts.Persistence.TicketingMode
// / .PublishStatus / .City exactly — the wire value is the ordinal, see coerceEnum.
const TICKETING_MODES: readonly TicketingMode[] = ['SingleOccurrence', 'DailyEntry', 'RecurringReservation'];
const PUBLISH_STATUSES: readonly PublishStatus[] = ['Draft', 'Published'];
const CITIES: readonly City[] = ['Sarajevo', 'Mostar', 'BanjaLuka', 'Tuzla', 'Zenica', 'Bihac', 'Brcko', 'Trebinje'];

export const toTicketingMode = (raw: unknown): TicketingMode => coerceEnum(raw, TICKETING_MODES, 'SingleOccurrence');
export const toPublishStatus = (raw: unknown): PublishStatus => coerceEnum(raw, PUBLISH_STATUSES, 'Published');
export const toCity = (raw: unknown): City => coerceEnum(raw, CITIES, 'Sarajevo');

/** Bosnian display label for each City — used by the location filter dropdown and the
 * product-details location row. */
export const CITY_LABELS: Readonly<Record<City, string>> = {
  Sarajevo: 'Sarajevo',
  Mostar: 'Mostar',
  BanjaLuka: 'Banja Luka',
  Tuzla: 'Tuzla',
  Zenica: 'Zenica',
  Bihac: 'Bihać',
  Brcko: 'Brčko',
  Trebinje: 'Trebinje',
};

/** All cities, in wire-ordinal order — source for every city `<select>` in this app. */
export const ALL_CITIES: readonly City[] = CITIES;

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
  latitude: number;
  longitude: number;
  city: City;
  images: ProductImage[];
  createdAt: string;
}

/**
 * `ticketingMode`/`status`/`city` arrive as integer ordinals, not names — see `coerceEnum`.
 * Normalizing here, once on the way in, is what keeps every downstream
 * `@switch (product.ticketingMode)` and `=== 'DailyEntry'` comparison working; without it they
 * silently compare a string literal against a number and never match.
 *
 * Lives on the model rather than inside `CatalogService` because `RecommendationService` returns
 * the same `Product` shape and must normalize it identically — two copies of this would be two
 * places for the ordinal mapping to drift.
 */
export function normalizeProduct(raw: Product): Product {
  return {
    ...raw,
    ticketingMode: toTicketingMode(raw.ticketingMode),
    status: toPublishStatus(raw.status),
    city: toCity(raw.city),
  };
}

export function normalizeCategory(raw: Category): Category {
  return { ...raw, ticketingMode: toTicketingMode(raw.ticketingMode) };
}

export interface ProductQuery {
  page?: number;
  pageSize?: number;
  fts?: string | null;
  categoryId?: number | null;
  city?: City | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
