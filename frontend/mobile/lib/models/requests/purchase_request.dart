class PurchaseLineItemRequest {
  final String? ticketTypeId;
  final int quantity;

  const PurchaseLineItemRequest({this.ticketTypeId, required this.quantity});

  Map<String, dynamic> toJson() => {
        if (ticketTypeId != null) 'ticketTypeId': ticketTypeId,
        'quantity': quantity,
      };
}

/// Completes a purchase the buyer has already paid for. Mirrors `PurchaseRequestValidator`
/// field-for-field — see [[00-workflow-and-testing]]: the backend validator is the source of truth,
/// the form rules in PaymentScreen are the UX mirror.
///
/// No card data, by design: with the Stripe provider the card goes straight from Stripe's own
/// payment sheet to Stripe and only identifiers come back here. [simulatedLast4] is the one
/// exception and it exists solely for the offline Mock gateway.
class PurchaseRequest {
  final String holdId;
  final List<PurchaseLineItemRequest> lineItems;

  /// Minted server-side by POST /purchases/payment-intent and echoed back.
  final String orderId;

  /// The provider intent the buyer confirmed, or the subscription reference for a monthly
  /// reservation.
  final String paymentIntentId;

  /// Mock provider only: "0000" simulates a decline. Null in Stripe mode.
  final String? simulatedLast4;

  const PurchaseRequest({
    required this.holdId,
    required this.lineItems,
    required this.orderId,
    required this.paymentIntentId,
    this.simulatedLast4,
  });

  Map<String, dynamic> toJson() => {
        'holdId': holdId,
        'lineItems': lineItems.map((e) => e.toJson()).toList(),
        'orderId': orderId,
        'paymentIntentId': paymentIntentId,
        if (simulatedLast4 != null) 'simulatedLast4': simulatedLast4,
      };
}
