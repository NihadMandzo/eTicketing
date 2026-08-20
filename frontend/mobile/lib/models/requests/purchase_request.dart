class PurchaseLineItemRequest {
  final String? ticketTypeId;
  final int quantity;

  const PurchaseLineItemRequest({this.ticketTypeId, required this.quantity});

  Map<String, dynamic> toJson() => {
        if (ticketTypeId != null) 'ticketTypeId': ticketTypeId,
        'quantity': quantity,
      };
}

/// Mirrors `PurchaseRequestValidator` field-for-field — see
/// [[00-workflow-and-testing]]: the backend validator is the source of
/// truth, these are just the UX mirror applied in PaymentScreen's form.
class PurchaseRequest {
  final String holdId;
  final List<PurchaseLineItemRequest> lineItems;
  final String cardNumber;
  final String cardExpiry;
  final String cardCvv;

  const PurchaseRequest({
    required this.holdId,
    required this.lineItems,
    required this.cardNumber,
    required this.cardExpiry,
    required this.cardCvv,
  });

  Map<String, dynamic> toJson() => {
        'holdId': holdId,
        'lineItems': lineItems.map((e) => e.toJson()).toList(),
        'cardNumber': cardNumber,
        'cardExpiry': cardExpiry,
        'cardCvv': cardCvv,
      };
}
