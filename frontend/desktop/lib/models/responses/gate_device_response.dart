/// One sector a gate device admits. `id` is the Sector's own id — the join row's id is an
/// implementation detail the client never needs.
class GateDeviceSectorResponse {
  final String id;
  final String name;

  const GateDeviceSectorResponse({required this.id, required this.name});

  factory GateDeviceSectorResponse.fromJson(Map<String, dynamic> json) => GateDeviceSectorResponse(
        id: json['id'] as String,
        name: json['name'] as String? ?? '',
      );
}

/// A registered gate scanner. Mirrors the backend's GateDeviceResponse.
///
/// Note there is no `apiKey` here, and there never will be: the server keeps only a hash, so the
/// plaintext exists exactly once, in [GateDeviceCreatedResponse].
class GateDeviceResponse {
  final String id;
  final String productId;
  final String name;

  /// Leading characters of the key, kept in the clear purely so two devices are tellable apart
  /// after the plaintext is gone. Not usable to authenticate.
  final String keyPrefix;

  /// True for a main-entrance device admitting every sector of [productId]; [sectors] is then empty.
  final bool allSectors;

  final bool isActive;
  final DateTime? lastSeenAt;
  final DateTime createdAt;
  final List<GateDeviceSectorResponse> sectors;

  const GateDeviceResponse({
    required this.id,
    required this.productId,
    required this.name,
    required this.keyPrefix,
    required this.allSectors,
    required this.isActive,
    this.lastSeenAt,
    required this.createdAt,
    this.sectors = const [],
  });

  factory GateDeviceResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['productId'] == null) throw const FormatException('Missing productId in payload');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt in payload');

    return GateDeviceResponse(
      id: json['id'] as String,
      productId: json['productId'] as String,
      name: json['name'] as String? ?? '',
      keyPrefix: json['keyPrefix'] as String? ?? '',
      allSectors: json['allSectors'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
      lastSeenAt: json['lastSeenAt'] == null ? null : DateTime.parse(json['lastSeenAt'] as String),
      createdAt: DateTime.parse(json['createdAt'] as String),
      sectors: (json['sectors'] as List? ?? [])
          .map((e) => GateDeviceSectorResponse.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

/// Returned by create and rotate-key, and by nothing else. [apiKey] is the only time the plaintext
/// is ever visible — show it, let the user copy it, and never persist it.
class GateDeviceCreatedResponse {
  final GateDeviceResponse device;
  final String apiKey;

  const GateDeviceCreatedResponse({required this.device, required this.apiKey});

  factory GateDeviceCreatedResponse.fromJson(Map<String, dynamic> json) => GateDeviceCreatedResponse(
        device: GateDeviceResponse.fromJson(json['device'] as Map<String, dynamic>),
        apiKey: json['apiKey'] as String? ?? '',
      );
}
