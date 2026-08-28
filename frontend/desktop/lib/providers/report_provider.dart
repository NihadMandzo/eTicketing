import 'dart:convert';

import 'package:dio/dio.dart';

import '../core/api_client.dart';
import '../models/api_error.dart';
import '../models/responses/report_responses.dart';
import 'api_exception.dart';
import 'base_provider.dart';

/// Which report is being asked for. The wire names are the C# `ReportTab` enum
/// members, sent as the `tab` query parameter of the export endpoint.
enum ReportTab {
  sales('Sales', 'Prodaja'),
  products('Products', 'Učinak Proizvoda'),
  redemption('Redemption', 'Iskorištenost Karata'),
  organizations('Organizations', 'Organizacije');

  const ReportTab(this.wireName, this.label);

  final String wireName;
  final String label;
}

/// The Izvještaji screen's API client.
///
/// Extends [BaseProvider] for its `send()` timeout handling only — reports have
/// no generic CRUD surface, so none of the inherited getAll/insert/update
/// methods are used. `String` is the id type by convention for the
/// Guid-keyed resources this service owns.
class ReportProvider extends BaseProvider<SalesReport, String> {
  ReportProvider() : super('reports');

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

  /// GET /api/reports/sales
  Future<SalesReport> getSales(DateTime from, DateTime to) async =>
      SalesReport.fromJson(await _get('reports/sales', from, to));

  /// GET /api/reports/products
  Future<ProductReport> getProducts(DateTime from, DateTime to) async =>
      ProductReport.fromJson(await _get('reports/products', from, to));

  /// GET /api/reports/redemption
  Future<RedemptionReport> getRedemption(DateTime from, DateTime to) async =>
      RedemptionReport.fromJson(await _get('reports/redemption', from, to));

  /// GET /api/reports/organizations
  Future<OrganizationReport> getOrganizations(DateTime from, DateTime to) async =>
      OrganizationReport.fromJson(await _get('reports/organizations', from, to));

  Future<Map<String, dynamic>> _get(String path, DateTime from, DateTime to) async {
    final response = await send(() => apiClient.get(path, queryParameters: {
          'from': _asDateOnly(from),
          'to': _asDateOnly(to),
        }));

    if (!_isSuccess(response.statusCode)) _handleError(response);

    return response.data as Map<String, dynamic>;
  }

  /// GET /api/reports/export — the rendered PDF's bytes.
  ///
  /// Mirrors `TicketPrintProvider.downloadFile`: the response comes back as raw
  /// bytes, so an error body has to be re-read as JSON before it can carry the
  /// API's Bosnian message (see [_decoded]). The server refuses this outright
  /// for OrganizationAdmin, which is why the screen hides the button for that
  /// role rather than relying on it.
  Future<List<int>> downloadPdf(ReportTab tab, DateTime from, DateTime to) async {
    final response = await send(() => apiClient.get<List<int>>(
          'reports/export',
          queryParameters: {
            'tab': tab.wireName,
            'from': _asDateOnly(from),
            'to': _asDateOnly(to),
          },
          options: Options(
            responseType: ResponseType.bytes,
            // Generous next to what a report actually costs, but a year-long
            // platform-wide export is several queries plus a render.
            receiveTimeout: const Duration(minutes: 2),
          ),
        ));

    final bytes = (response.data as List<dynamic>?)?.cast<int>();

    if (!_isSuccess(response.statusCode)) {
      _handleError(_decoded(response.requestOptions, response.statusCode, bytes));
    }

    return bytes ?? const [];
  }

  /// Re-reads an errored bytes response as JSON, so a refused export reports the
  /// API's message instead of a byte array.
  Response _decoded(RequestOptions request, int? statusCode, List<int>? bytes) {
    dynamic body;
    try {
      body = jsonDecode(utf8.decode(bytes ?? const []));
    } catch (_) {
      body = null;
    }
    return Response(requestOptions: request, statusCode: statusCode, data: body);
  }

  /// The API binds `DateOnly`, which rejects a full ISO timestamp.
  static String _asDateOnly(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
