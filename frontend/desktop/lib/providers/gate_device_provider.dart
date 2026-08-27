import 'package:dio/dio.dart';

import '../models/api_error.dart';
import '../models/requests/gate_device_upsert_request.dart';
import '../models/responses/gate_device_response.dart';
import '../core/api_client.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// `/api/gate-devices` — registration and lifecycle of the physical gate scanners (see `IoT/`).
///
/// The devices themselves never talk to this provider; they authenticate with their own API key
/// against `/api/gate/*`. This is only the back-office side.
class GateDeviceProvider extends BaseProvider<GateDeviceResponse, String> {
  GateDeviceProvider() : super('gate-devices');

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

  /// POST /api/gate-devices — the response carries the plaintext key, shown once.
  Future<GateDeviceCreatedResponse> createDevice(GateDeviceUpsertRequest request) async {
    final response = await send(() => apiClient.post('gate-devices', data: request.toJson()));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return GateDeviceCreatedResponse.fromJson(response.data as Map<String, dynamic>);
  }

  Future<GateDeviceResponse> updateDevice(String id, GateDeviceUpsertRequest request) =>
      update(id, request.toJson(), fromJson: GateDeviceResponse.fromJson);

  /// POST /api/gate-devices/{id}/rotate-key — mints a replacement and kills the old key instantly.
  Future<GateDeviceCreatedResponse> rotateKey(String id) async {
    final response = await send(() => apiClient.post('gate-devices/$id/rotate-key'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return GateDeviceCreatedResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
