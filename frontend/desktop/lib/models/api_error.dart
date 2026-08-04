class ApiError {
  final String? errorCode;
  final String? message;
  final Map<String, List<String>>? errors;
  final String? traceId;

  const ApiError({
    this.errorCode,
    this.message,
    this.errors,
    this.traceId,
  });

  factory ApiError.fromJson(Map<String, dynamic> json) {
    Map<String, List<String>>? errors;

    if (json['errors'] is Map) {
      errors = (json['errors'] as Map).map((key, value) {
        final listValue = value is List ? value : [value];
        return MapEntry(
          key.toString(),
          listValue.map((e) => e.toString()).toList(),
        );
      });
    }

    return ApiError(
      errorCode: json['errorCode'] as String?,
      message: json['message'] as String?,
      errors: errors,
      traceId: json['traceId'] as String?,
    );
  }

  /// Returns a flat, human-readable error message.
  String get displayMessage {
    if (errors != null && errors!.isNotEmpty) {
      final messages = errors!.values.expand((e) => e).toList();
      return messages.join('\n');
    }
    
    if (message != null && message!.isNotEmpty) {
      return message!;
    }
    
    if (errorCode != null && errorCode!.isNotEmpty) {
      return errorCode!;
    }
    
    return 'An unexpected error occurred.';
  }
}
