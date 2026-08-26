import 'dart:convert';

import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/requests/create_ticket_print_batch_request.dart';
import '../models/responses/ticket_print_batch_response.dart';
import '../models/responses/ticket_print_options_response.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Physical-ticket export: what can be printed, requesting a batch, watching it
/// render, and collecting the finished PDF.
class TicketPrintProvider extends BaseProvider<TicketPrintBatchResponse, String> {
  TicketPrintProvider() : super('ticket-print-batches');

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

  /// GET /api/ticket-print-batches/options — sectors, remaining capacity and
  /// where stub numbering resumes. [date] applies to DailyEntry products only,
  /// where capacity is tracked per calendar day.
  Future<TicketPrintOptionsResponse> getOptions(String productId, {DateTime? date}) async {
    final response = await send(() => apiClient.get('ticket-print-batches/options', queryParameters: {
          'productId': productId,
          if (date != null) 'date': _asDateOnly(date),
        }));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return TicketPrintOptionsResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// POST /api/ticket-print-batches — returns as soon as the tickets are
  /// committed; the PDF is rendered afterwards, off the request thread.
  Future<TicketPrintBatchResponse> create(CreateTicketPrintBatchRequest request) async {
    final response = await send(() => apiClient.post('ticket-print-batches', data: request.toJson()));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return TicketPrintBatchResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// GET /api/ticket-print-batches/{id} — status poll.
  Future<TicketPrintBatchResponse> getBatch(String id) async {
    final response = await send(() => apiClient.get('ticket-print-batches/$id'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return TicketPrintBatchResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// GET /api/ticket-print-batches/outstanding — every batch of the caller's
  /// organization still worth showing, newest first. Backs the top-bar badge.
  Future<List<TicketPrintBatchResponse>> getOutstanding() async {
    final response = await send(() => apiClient.get('ticket-print-batches/outstanding'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return (response.data as List<dynamic>)
        .map((e) => TicketPrintBatchResponse.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// GET /api/ticket-print-batches/latest — resumes a render the organizer
  /// started before navigating away. Null when the product was never exported.
  Future<TicketPrintBatchResponse?> getLatestForProduct(String productId) async {
    final response = await send(
        () => apiClient.get('ticket-print-batches/latest', queryParameters: {'productId': productId}));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    // A product that has never been exported comes back 204 with no body. Dio
    // surfaces an empty body as the empty *string*, not as null, so checking for
    // null alone would send `''` into a Map cast and blow up on a perfectly
    // normal response — hence the shape check rather than a null check.
    final data = response.data;
    return data is Map<String, dynamic> ? TicketPrintBatchResponse.fromJson(data) : null;
  }

  /// GET /api/ticket-print-batches/{id}/file — hands over the sheet and
  /// destroys the server's copy in the same transaction, so this succeeds
  /// exactly once per batch.
  ///
  /// The generous receive timeout is deliberate: the global one is 10s, which a
  /// several-hundred-page PDF over a slow link will blow through even though
  /// nothing is being generated at this point.
  Future<List<int>> downloadFile(String id) async {
    final response = await send(() => apiClient.get<List<int>>(
          'ticket-print-batches/$id/file',
          options: Options(
            responseType: ResponseType.bytes,
            receiveTimeout: const Duration(minutes: 5),
          ),
        ));

    final bytes = (response.data as List<dynamic>?)?.cast<int>();

    if (!_isSuccess(response.statusCode)) _handleError(_decoded(response.requestOptions, response.statusCode, bytes));

    return bytes ?? const [];
  }

  /// POST /api/ticket-print-batches/{id}/retry — asks for the paper again. The
  /// tickets already exist and their capacity is already claimed, so this never
  /// mints or reserves anything.
  Future<TicketPrintBatchResponse> retry(String id) async {
    final response = await send(() => apiClient.post('ticket-print-batches/$id/retry'));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return TicketPrintBatchResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// Re-reads an errored bytes response as JSON, so a failed download reports
  /// the API's Bosnian message instead of a byte array. The response body came
  /// back as raw bytes because of `responseType: bytes` above, which is right
  /// for a PDF and useless for a ProblemDetails payload.
  Response _decoded(RequestOptions request, int? statusCode, List<int>? bytes) {
    dynamic body;
    try {
      body = jsonDecode(utf8.decode(bytes ?? const []));
    } catch (_) {
      body = null;
    }
    return Response(requestOptions: request, statusCode: statusCode, data: body);
  }

  static String _asDateOnly(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
