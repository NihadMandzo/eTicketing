import '../api_enum.dart';

/// Mirrors `eTicketing.Ticketing.Data.Entities.SubscriptionStatus`, in declaration order --
/// the wire value is the ordinal, same as every other enum in this app.
const subscriptionStatusNames = ['Active', 'Cancelled', 'PastDue'];

const subscriptionStatusLabels = <String, String>{
  'Active': 'Aktivna',
  'Cancelled': 'Otkazana',
  'PastDue': 'Neplaćena',
};

String subscriptionStatusFromJson(dynamic value) =>
    enumNameFromJson(value, subscriptionStatusNames, 'Active');

class SubscriptionResponse {
  final String id;
  final String sectorId;
  final String sectorName;
  final String productId;
  final String status;
  final String currentPeriodStart;
  final String currentPeriodEnd;
  final DateTime? nextRenewalAt;

  /// The buyer cancelled but the period they paid for is still running, so [status] is still
  /// 'Active'. The UI has to say so, or a cancellation looks like it did not take.
  final bool cancelAtPeriodEnd;
  final double pricePerPeriod;

  const SubscriptionResponse({
    required this.id,
    required this.sectorId,
    required this.sectorName,
    required this.productId,
    required this.status,
    required this.currentPeriodStart,
    required this.currentPeriodEnd,
    required this.nextRenewalAt,
    required this.cancelAtPeriodEnd,
    required this.pricePerPeriod,
  });

  String get statusLabel => subscriptionStatusLabels[status] ?? status;

  bool get isCancellable => status == 'Active' && !cancelAtPeriodEnd;

  factory SubscriptionResponse.fromJson(Map<String, dynamic> json) => SubscriptionResponse(
        id: json['id'] as String,
        sectorId: json['sectorId'] as String,
        sectorName: json['sectorName'] as String? ?? '',
        productId: json['productId'] as String,
        status: subscriptionStatusFromJson(json['status']),
        currentPeriodStart: json['currentPeriodStart'] as String? ?? '',
        currentPeriodEnd: json['currentPeriodEnd'] as String? ?? '',
        nextRenewalAt:
            json['nextRenewalAt'] == null ? null : DateTime.parse(json['nextRenewalAt'] as String),
        cancelAtPeriodEnd: json['cancelAtPeriodEnd'] as bool? ?? false,
        pricePerPeriod: (json['pricePerPeriod'] as num?)?.toDouble() ?? 0,
      );
}
