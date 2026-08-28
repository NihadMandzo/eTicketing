import { coerceEnum } from '../utils/api-enum.util';
import { Product } from './catalog.models';

/**
 * Which strategy actually produced a list. The API reports it so the row can be titled honestly —
 * calling a popularity list "Preporučeno za vas" on a brand-new account would be a lie, and during
 * a demo it hides which branch of the fallback chain ran.
 */
export type RecommendationSource = 'Personalized' | 'ContentBased' | 'Popular';

// Declaration order must match eTicketing.Catalog.Business.Recommendations.RecommendationSource
// exactly — the wire value is the ordinal, see coerceEnum.
const RECOMMENDATION_SOURCES: readonly RecommendationSource[] = [
  'Personalized',
  'ContentBased',
  'Popular',
];

export const toRecommendationSource = (raw: unknown): RecommendationSource =>
  coerceEnum(raw, RECOMMENDATION_SOURCES, 'Popular');

export interface RecommendationResult {
  items: Product[];
  source: RecommendationSource;
}

/** Row heading per strategy. Personalized and ContentBased deliberately share one: the distinction
 * is real to us but meaningless to a shopper, who only needs to know the row is about them.
 *
 * 'Popular' is titled without a place on purpose. The API ranks this row across the whole catalog —
 * it reaches Popular precisely when it knows nothing about the visitor, including where they are —
 * so promising "u vašem gradu" would be the exact dishonesty `source` exists to prevent. */
export const RECOMMENDATION_TITLES: Readonly<Record<RecommendationSource, string>> = {
  Personalized: 'Preporučeno za vas',
  ContentBased: 'Preporučeno za vas',
  Popular: 'Popularno',
};
