import 'purchase_request.dart';

/// Prices the hold server-side and creates the payment object the buyer confirms.
/// Mirrors `CreatePaymentIntentRequestValidator` field-for-field.
class CreatePaymentIntentRequest {
  final String holdId;
  final List<PurchaseLineItemRequest> lineItems;

  const CreatePaymentIntentRequest({required this.holdId, required this.lineItems});

  Map<String, dynamic> toJson() => {
        'holdId': holdId,
        'lineItems': lineItems.map((item) => item.toJson()).toList(),
      };
}
