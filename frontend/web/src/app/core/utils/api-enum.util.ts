/**
 * The backend serializes every C# enum with System.Text.Json's **default**
 * converter, which writes the integer ordinal — `"ticketingMode": 0`,
 * `"status": 1` — not the name. (`eTicketing.Contracts` deliberately keeps
 * it that way: the Flutter desktop client parses those ordinals directly,
 * see `desktop/lib/models/enums/ticketing_mode.dart`.)
 *
 * These models keep the readable string unions, because that's what the
 * templates `@switch` on, so every enum-valued field is coerced exactly
 * once — at the service boundary, on the way in. Skipping that is invisible
 * in TypeScript (a type annotation is not a runtime cast), which is how a
 * perfectly successful `GET /products/{id}` ended up rendering an empty
 * purchase card: `product.ticketingMode` held `0`, and `0` matches none of
 * the `@case ('SingleOccurrence')` branches.
 *
 * Both ordinals and names are accepted so nothing here breaks if the
 * backend ever adds a `JsonStringEnumConverter`.
 */
export function coerceEnum<T extends string>(raw: unknown, names: readonly T[], fallback: T): T {
  if (typeof raw === 'number') return names[raw] ?? fallback;
  if (typeof raw === 'string') {
    // A numeric string is what an ordinal survives as through a query-string
    // or form round-trip.
    const ordinal = Number(raw);
    if (raw.trim() !== '' && Number.isInteger(ordinal)) return names[ordinal] ?? fallback;
    return names.includes(raw as T) ? (raw as T) : fallback;
  }
  return fallback;
}
