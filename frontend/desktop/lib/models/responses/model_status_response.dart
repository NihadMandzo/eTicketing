/// One past training run of the recommendation model.
class ModelSnapshotResponse {
  final String id;
  final DateTime trainedAt;
  final int interactionCount;
  final int userCount;
  final int productCount;
  final int trainingDurationMs;
  final bool isActive;

  const ModelSnapshotResponse({
    required this.id,
    required this.trainedAt,
    required this.interactionCount,
    required this.userCount,
    required this.productCount,
    required this.trainingDurationMs,
    required this.isActive,
  });

  factory ModelSnapshotResponse.fromJson(Map<String, dynamic> json) => ModelSnapshotResponse(
        id: json['id'] as String,
        trainedAt: DateTime.parse(json['trainedAt'] as String),
        interactionCount: json['interactionCount'] as int? ?? 0,
        userCount: json['userCount'] as int? ?? 0,
        productCount: json['productCount'] as int? ?? 0,
        trainingDurationMs: json['trainingDurationMs'] as int? ?? 0,
        isActive: json['isActive'] as bool? ?? false,
      );
}

/// Everything the "Preporuke" screen shows.
///
/// [trainedAt] and [isTrained] can legitimately disagree: the first describes the last recorded
/// training run, the second whether a model is loaded and scoring in the service right now. They
/// diverge exactly when a snapshot exists but its stored model could not be loaded back — which is
/// precisely the situation worth being able to see on screen.
class ModelStatusResponse {
  final DateTime? trainedAt;
  final bool isTrained;
  final int modelInteractionCount;
  final int modelUserCount;
  final int modelProductCount;
  final int trainingDurationMs;
  final int totalInteractions;
  final int viewCount;
  final int purchaseCount;
  final int distinctUsers;
  final int distinctProducts;
  final List<ModelSnapshotResponse> history;

  const ModelStatusResponse({
    required this.trainedAt,
    required this.isTrained,
    required this.modelInteractionCount,
    required this.modelUserCount,
    required this.modelProductCount,
    required this.trainingDurationMs,
    required this.totalInteractions,
    required this.viewCount,
    required this.purchaseCount,
    required this.distinctUsers,
    required this.distinctProducts,
    required this.history,
  });

  /// New interactions recorded since the model was last trained — the number that answers "is it
  /// worth retraining right now".
  int get pendingInteractions =>
      (totalInteractions - modelInteractionCount).clamp(0, totalInteractions);

  factory ModelStatusResponse.fromJson(Map<String, dynamic> json) => ModelStatusResponse(
        trainedAt: json['trainedAt'] == null ? null : DateTime.parse(json['trainedAt'] as String),
        isTrained: json['isTrained'] as bool? ?? false,
        modelInteractionCount: json['modelInteractionCount'] as int? ?? 0,
        modelUserCount: json['modelUserCount'] as int? ?? 0,
        modelProductCount: json['modelProductCount'] as int? ?? 0,
        trainingDurationMs: json['trainingDurationMs'] as int? ?? 0,
        totalInteractions: json['totalInteractions'] as int? ?? 0,
        viewCount: json['viewCount'] as int? ?? 0,
        purchaseCount: json['purchaseCount'] as int? ?? 0,
        distinctUsers: json['distinctUsers'] as int? ?? 0,
        distinctProducts: json['distinctProducts'] as int? ?? 0,
        history: (json['history'] as List<dynamic>? ?? [])
            .map((item) => ModelSnapshotResponse.fromJson(item as Map<String, dynamic>))
            .toList(),
      );
}
