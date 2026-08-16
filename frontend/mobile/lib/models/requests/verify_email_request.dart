class VerifyEmailRequest {
  final String code;

  const VerifyEmailRequest({required this.code});

  Map<String, dynamic> toJson() => {'code': code};
}
