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

  /**
   * How many admissions are still on sale, read off the live Redis counter by the backend on the
   * public buyer path (`GET /sectors`). See `SectorResponse.RemainingCapacity`.
   *
   * `null` means *unknown*, which is emphatically not the same as zero and must never be rendered
   * as sold out: the backend returns null for a DailyEntry sector queried without a date (capacity
   * is per `(sector, date)` there, so one number would be a lie) and on the organizer/staff lists.
   * Treating unknown as sold out would hide sectors that are perfectly on sale.
   */
  remainingCapacity: number | null;
}

/** True only when availability is actually known and exhausted — mirrors `SectorResponse.IsSoldOut`
 * on the backend, including its refusal to let a null read as sold out. */
export function isSoldOut(sector: Sector): boolean {
  return sector.remainingCapacity === 0;
}

export interface HoldSectorRequest {
  quantity: number;
  date?: string | null;
}

export interface HoldSectorResponse {
  holdId: string;
  expiresAt: string;
}
