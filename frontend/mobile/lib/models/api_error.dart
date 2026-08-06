class ApiError {
  /// Matches the backend's `Result`/`Error` business-failure shape: `{ code, message }`.
  final String? code;

  /// Present on both the `{ code, message }` shape and ASP.NET Core's standard
  /// `ValidationProblem` shape (`{ title, status, errors }`) — used as the
  /// "title" there.
  final String? message;

  /// Field-level validation errors, present on FluentValidation's
  /// `ValidationProblem` responses: `{ "FieldName": ["message", ...] }`.
  final Map<String, List<String>>? errors;

  const ApiError({this.code, this.message, this.errors});

  factory ApiError.fromJson(Map<String, dynamic> json) {
    Map<String, List<String>>? errors;

    if (json['errors'] is Map) {
      errors = (json['errors'] as Map).map((key, value) {
        final listValue = value is List ? value : [value];
        return MapEntry(key.toString(), listValue.map((e) => e.toString()).toList());
      });
    }

    return ApiError(
      code: json['code'] as String?,
      message: (json['message'] ?? json['title']) as String?,
      errors: errors,
    );
  }

  /// Returns a flat, human-readable error message.
  String get displayMessage {
    if (errors != null && errors!.isNotEmpty) {
      return errors!.values.expand((e) => e).join('\n');
    }
    if (message != null && message!.isNotEmpty) return message!;
    if (code != null && code!.isNotEmpty) return code!;
    return 'Došlo je do neočekivane greške.';
  }
}
