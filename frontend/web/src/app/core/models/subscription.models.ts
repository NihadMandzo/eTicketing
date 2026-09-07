import { coerceEnum } from '../utils/api-enum.util';

export type SubscriptionStatus = 'Active' | 'Cancelled' | 'PastDue';

// Declaration order must match eTicketing.Ticketing.Data.Entities.SubscriptionStatus exactly —
// the wire value is the ordinal, see coerceEnum.
const SUBSCRIPTION_STATUSES: readonly SubscriptionStatus[] = ['Active', 'Cancelled', 'PastDue'];

export const toSubscriptionStatus = (raw: unknown): SubscriptionStatus =>
  coerceEnum(raw, SUBSCRIPTION_STATUSES, 'Active');

export const SUBSCRIPTION_STATUS_LABELS: Record<SubscriptionStatus, string> = {
  Active: 'Aktivna',
  Cancelled: 'Otkazana',
  PastDue: 'Neplaćena',
};

export interface Subscription {
  id: string;
  sectorId: string;
  sectorName: string;
  productId: string;
  status: SubscriptionStatus;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  nextRenewalAt: string | null;
  /** The buyer cancelled but the period they paid for is still running, so `status` is still
   * 'Active'. The UI must say this explicitly, or a cancellation looks like it did not take. */
  cancelAtPeriodEnd: boolean;
  cancelledAt: string | null;
  pricePerPeriod: number;
}
