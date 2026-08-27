import 'dart:convert';

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

  /// GET /api/tickets/{id}/pdf — the buyer's own ticket, rendered on the spot.
  ///
  /// Goes through [apiClient] rather than handing the URL to the OS browser:
  /// the session is an httpOnly cookie living in this client's jar, so an
  /// external request would arrive unauthenticated and get a 401. Nothing is
  /// queued or generated in the background — the sheet is rendered per request,
  /// so this returns the finished bytes.
  Future<List<int>> downloadTicketPdf(String ticketId) async {
    final response = await apiClient.get<List<int>>(
      'Tickets/$ticketId/pdf',
      options: Options(
        responseType: ResponseType.bytes,
        // The global receiveTimeout is 10s, which is generous for JSON but tight
        // for a rendered PDF on a slow connection.
        receiveTimeout: const Duration(seconds: 60),
      ),
    );

    if (!_isSuccess(response.statusCode)) {
      // An error body came back as raw bytes because of responseType above —
      // decode it so _handleError can read the API's Bosnian message instead of
      // reporting a byte array.
      _handleError(_decoded(response));
    }

    return response.data ?? const [];
  }

  /// Re-reads an errored bytes response as JSON so the shared error path works.
  Response _decoded(Response<List<int>> response) {
    dynamic body;
    try {
      body = jsonDecode(utf8.decode(response.data ?? const []));
    } catch (_) {
      body = null;
    }
    return Response(
      requestOptions: response.requestOptions,
      statusCode: response.statusCode,
      data: body,
    );
  }
}
