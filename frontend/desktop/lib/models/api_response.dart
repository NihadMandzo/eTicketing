class ApiResponse {
  final int statusCode;
  final String message;

  const ApiResponse({
    required this.statusCode,
    required this.message,
  });

  factory ApiResponse.fromJson(Map<String, dynamic> json) {
    return ApiResponse(
      statusCode: json['statusCode'] as int? ?? 0,
      message: json['message'] as String? ?? '',
    );
  }
}
