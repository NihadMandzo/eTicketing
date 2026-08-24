import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/ticket_validation_response.dart';
import '../models/responses/validation_product_response.dart';
import 'api_exception.dart';

/// Gate validation, for `OrganizationAdmin`/`OrganizationSuperAdmin` (and
/// platform staff). Mirrors the shape of the other services in this folder —
/// no shared base class, same `_isSuccess`/`_handleError` pair.
class ValidationService {
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

  /// GET /api/tickets/validation/products — products of the caller's own
  /// organization with tickets admitting entry TODAY.
  Future<List<ValidationProductResponse>> getProductsForToday() async {
    final response = await apiClient.get('Tickets/validation/products');
    if (!_isSuccess(response.statusCode)) _handleError(response);

    return (response.data as List)
        .map((item) => ValidationProductResponse.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  /// POST /api/tickets/validate — always against a specific product, never
  /// "is this ticket valid in general".
  ///
  /// A ticket that turns out to be invalid comes back as a normal 200 with
  /// `isValid: false`; only a wrong role (403) or a concurrent scan of the
  /// same ticket (409) throw. Callers must handle both.
  Future<TicketValidationResponse> validate({
    required String productId,
    required String code,
  }) async {
    final response = await apiClient.post('Tickets/validate', data: {
      'productId': productId,
      'code': code,
    });
    if (!_isSuccess(response.statusCode)) _handleError(response);
    return TicketValidationResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
