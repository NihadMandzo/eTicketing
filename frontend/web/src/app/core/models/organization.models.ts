/**
 * The organizer behind a product, as shown in the "Organizator" card on the
 * product-details page. Mirrors `OrganizationPublicResponse` on the backend and
 * `lib/models/responses/organization_response.dart` on mobile — widen all three
 * together.
 *
 * `userCount` and `isActive` are deliberately left out: they are back-office
 * facts about the account, not information a customer browsing an event has any
 * use for.
 */
export interface Organization {
  id: string;
  name: string;
  description: string;
  address: string;
  phoneNumber: string;
  email: string;
  website: string | null;
  logoUrl: string | null;
}
