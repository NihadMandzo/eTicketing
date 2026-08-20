/**
 * Only the fields the storefront actually reads (the organizer name on the
 * product-details page) — the backend response carries more (address,
 * phone, logo, ...). Mirrors `lib/models/responses/organization_response.dart`
 * on mobile.
 */
export interface Organization {
  id: string;
  name: string;
}
