import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/purchase_request.dart';
import '../models/responses/paged_result.dart';
import '../models/responses/purchase_response.dart';
import '../models/responses/ticket_response.dart';
import 'api_exception.dart';

/// The synchronous purchase critical path + own-ticket listing — mirrors
/// `PurchaseService` in `frontend/web`.
class PurchaseService {
  bool _isSuccess(int? code) => code != null && code >= 200 && code < 300;

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

  /// POST /api/purchases.
  Future<PurchaseResponse> purchase(PurchaseRequest request) async {
    final response = await apiClient.post('Purchases', data: request.toJson());
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return PurchaseResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// GET /api/tickets/mine — the caller's own tickets, any status.
  Future<PagedResult<TicketResponse>> getMyTickets({int page = 0, int pageSize = 50}) async {
    final response = await apiClient.get('Tickets/mine', queryParameters: {'page': page, 'pageSize': pageSize});
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return PagedResult.fromJson(response.data as Map<String, dynamic>, TicketResponse.fromJson);
  }
}
