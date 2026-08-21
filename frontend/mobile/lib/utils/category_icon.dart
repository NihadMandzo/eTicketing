import 'package:flutter/material.dart';

/// Categories are DB-driven (SuperAdmin/organizer-configurable), so there's
/// no fixed enum to switch on — icons are picked by matching the category
/// **name** against a small curated keyword table instead. First match
/// wins; anything unmatched (including any future organizer-created
/// category) falls back to a generic calendar icon. Mirrors
/// `category-icon.util.ts` on web — keep the two keyword tables in sync if
/// either changes. Uses only built-in Material icons, no new dependency.
// 'it' is a plain substring of common Bosnian words ("Kredit", "Identitet", ...), so unlike every
// other keyword below it needs word boundaries — mirrors web's `\bit\b` in category-icon.util.ts.
final RegExp _wholeWordIt = RegExp(r'\bit\b');

IconData categoryIcon(String name) {
  final lower = name.toLowerCase();
  if (_matches(lower, ['muzik', 'koncert', 'festival'])) return Icons.music_note_rounded;
  if (_matches(lower, ['sport', 'utakmic'])) return Icons.emoji_events_rounded;
  if (_matches(lower, ['tehnolog', 'tech', 'konferenc']) || _wholeWordIt.hasMatch(lower)) return Icons.memory_rounded;
  if (_matches(lower, ['teatar', 'pozorišt', 'pozorist', 'predstav', 'film', 'kino'])) return Icons.theaters_rounded;
  if (_matches(lower, ['galeri', 'umjetnost', 'izložb', 'izlozb'])) return Icons.palette_rounded;
  if (_matches(lower, ['muze'])) return Icons.museum_rounded;
  if (_matches(lower, ['park'])) return Icons.local_parking_rounded;
  if (_matches(lower, ['hrana', 'food', 'gastro', 'restoran'])) return Icons.restaurant_rounded;
  return Icons.confirmation_number_rounded;
}

bool _matches(String haystack, List<String> keywords) => keywords.any(haystack.contains);

/// A single glyph revealing what a category is — a real uploaded [iconUrl]
/// image when the organizer has set one, otherwise a curated icon picked by
/// [categoryIcon]. Renders bare (no circle/background) so callers can drop
/// it into whatever badge container they already have.
class CategoryIconGlyph extends StatelessWidget {
  final String name;
  final String? iconUrl;
  final double size;
  final Color? color;

  const CategoryIconGlyph({super.key, required this.name, this.iconUrl, this.size = 18, this.color});

  @override
  Widget build(BuildContext context) {
    final url = iconUrl;
    if (url != null && url.isNotEmpty) {
      return ClipRRect(
        borderRadius: BorderRadius.circular(size / 2),
        child: Image.network(
          url,
          width: size,
          height: size,
          fit: BoxFit.cover,
          errorBuilder: (_, _, _) => Icon(categoryIcon(name), size: size, color: color),
        ),
      );
    }
    return Icon(categoryIcon(name), size: size, color: color);
  }
}
