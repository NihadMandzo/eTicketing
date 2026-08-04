import '../models/api_error.dart';

class ApiException implements Exception {
  final int statusCode;
  final ApiError apiError;

  const ApiException({
    required this.statusCode,
    required this.apiError,
  });

  @override
  String toString() => 'ApiException($statusCode): ${apiError.displayMessage}';
}
