import '../models/responses/admin_user_response.dart';
import 'base_provider.dart';

/// Provider for the /api/Admin endpoint (SuperAdmin users list).
class AdminProvider extends BaseProvider<AdminUserResponse> {
  AdminProvider() : super('Admin');
}

/// Provider for the /api/Organizations/users endpoint (Org users list).
class OrganizationUsersProvider extends BaseProvider<AdminUserResponse> {
  OrganizationUsersProvider() : super('Organizations/users');
}
