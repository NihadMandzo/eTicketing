import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/ticket_type_upsert_request.dart';
import '../models/responses/ticket_type_response.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Scoped to one Sector — `sectors/{sectorId}/ticket-types`, per the
/// backend's nested route. Not `BaseProvider.getAll()` for reads (that
/// expects a `PagedResult<T>` body); the backend returns a plain array
/// here since a Sector realistically has a handful of TicketTypes at most
/// (e.g. Dijete/Student/Odrasli), not a paginated list.
class TicketTypeProvider extends BaseProvider<TicketTypeResponse, String> {
  final String sectorId;

  TicketTypeProvider(this.sectorId) : super('sectors/$sectorId/ticket-types');

  Never _handleError(Response response) {
    ApiError apiError;
    try {
      final body = response.data;
      apiError = body is Map<String, dynamic> ? ApiError.fromJson(body) : ApiError(message: body?.toString());
    } catch (_) {
      apiError = ApiError(message: response.data?.toString());
    }
    throw ApiException(statusCode: response.statusCode ?? 0, apiError: apiError);
  }

  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

  Future<TicketTypeResponse> createTicketType(TicketTypeUpsertRequest request) =>
      insert(request.toJson(), fromJson: TicketTypeResponse.fromJson);

  Future<TicketTypeResponse> updateTicketType(String id, TicketTypeUpsertRequest request) =>
      update(id, request.toJson(), fromJson: TicketTypeResponse.fromJson);

  /// GET /sectors/{sectorId}/ticket-types — plain array, no paging.
  Future<List<TicketTypeResponse>> getBySector() async {
    final response = await send(() => apiClient.get('sectors/$sectorId/ticket-types'));
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return (response.data as List).map((e) => TicketTypeResponse.fromJson(e as Map<String, dynamic>)).toList();
  }
}
