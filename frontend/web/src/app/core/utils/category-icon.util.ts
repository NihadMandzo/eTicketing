/**
 * Categories are DB-driven (SuperAdmin/organizer-configurable, see
 * category-chips.component.ts's doc comment), so there's no fixed enum to
 * switch on — icons are picked by matching the category **name** against a
 * small curated keyword table instead. First match wins; anything
 * unmatched (including any future organizer-created category) falls back
 * to a generic calendar icon. Mirrors `lib/utils/category_icon.dart` on
 * mobile — keep the two keyword tables in sync if either changes.
 */
export type CategoryIconKey = 'music' | 'award' | 'cpu' | 'film' | 'image' | 'map-pin' | 'parking' | 'coffee' | 'calendar';

const KEYWORD_TABLE: Array<[RegExp, CategoryIconKey]> = [
  [/muzik|koncert|festival/i, 'music'],
  [/sport|utakmic/i, 'award'],
  [/tehnolog|\bit\b|tech|konferenc/i, 'cpu'],
  [/teatar|pozori|predstav|film|kino/i, 'film'],
  [/galeri|umjetnost|izlo/i, 'image'],
  [/muze/i, 'map-pin'],
  [/park/i, 'parking'],
  [/hrana|food|gastro|restoran/i, 'coffee'],
];

export function categoryIconKey(name: string): CategoryIconKey {
  for (const [pattern, key] of KEYWORD_TABLE) {
    if (pattern.test(name)) return key;
  }
  return 'calendar';
}
