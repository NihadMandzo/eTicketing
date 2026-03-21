import '../models/requests/organization_update_request.dart';
import '../models/responses/organization_response.dart';
import 'base_provider.dart';

class OrganizationProvider extends BaseProvider<OrganizationResponse> {
  OrganizationProvider() : super('Organizations');

  Future<OrganizationResponse> getOrganization(int id) {
    return getById(id, fromJson: OrganizationResponse.fromJson);
  }

  Future<OrganizationResponse> updateOrganization(
      int id, OrganizationUpdateRequest request) {
    return update(id, request, fromJson: OrganizationResponse.fromJson);
  }
}
