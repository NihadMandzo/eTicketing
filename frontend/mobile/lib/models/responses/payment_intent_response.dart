/// Mirrors `PurchaseIntentResponse` in eTicketing.Ticketing.Business.Purchases.
///
/// [provider] and [publishableKey] come from the server rather than a `--dart-define` so switching
/// payment providers, or rotating the key, needs no rebuild of this app.
class PaymentIntentResponse {
  final String provider;
  final String? publishableKey;
  final String orderId;
  final String intentId;
  final String? clientSecret;

  /// Authoritative, computed by the backend from the held sector. This app never states a price.
  final double amount;
  final String currency;
  final bool isSubscription;

  const PaymentIntentResponse({
    required this.provider,
    required this.publishableKey,
    required this.orderId,
    required this.intentId,
    required this.clientSecret,
    required this.amount,
    required this.currency,
    required this.isSubscription,
  });

  bool get isMock => provider == 'Mock';

  factory PaymentIntentResponse.fromJson(Map<String, dynamic> json) => PaymentIntentResponse(
        provider: json['provider'] as String? ?? 'Mock',
        publishableKey: json['publishableKey'] as String?,
        orderId: json['orderId'] as String,
        intentId: json['intentId'] as String,
        clientSecret: json['clientSecret'] as String?,
        amount: (json['amount'] as num).toDouble(),
        currency: json['currency'] as String? ?? 'eur',
        isSubscription: json['isSubscription'] as bool? ?? false,
      );
}
