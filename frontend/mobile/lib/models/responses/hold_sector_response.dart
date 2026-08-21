class HoldSectorResponse {
  final String holdId;
  final DateTime expiresAt;

  const HoldSectorResponse({required this.holdId, required this.expiresAt});

  factory HoldSectorResponse.fromJson(Map<String, dynamic> json) {
    return HoldSectorResponse(
      holdId: json['holdId'] as String,
      expiresAt: DateTime.tryParse(json['expiresAt'] as String? ?? '') ?? DateTime.now(),
    );
  }
}
