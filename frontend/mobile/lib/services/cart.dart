import 'package:flutter/material.dart';

class CartLineItem {
  final String? ticketTypeId;
  final String? ticketTypeName;
  final int quantity;
  final double unitPrice;

  const CartLineItem({this.ticketTypeId, this.ticketTypeName, required this.quantity, required this.unitPrice});

  double get total => quantity * unitPrice;
}

/// One Redis hold = one Sector — selecting quantities across several Sectors
/// of the same Product ends up with one hold (and, at checkout, one separate
/// POST /purchases order) per Sector. Mirrors `CartHoldGroup` in
/// `frontend/web`.
class CartHoldGroup {
  final String holdId;
  final String sectorId;
  final String sectorName;
  final List<CartLineItem> lineItems;

  const CartHoldGroup({required this.holdId, required this.sectorId, required this.sectorName, required this.lineItems});

  double get total => lineItems.fold(0, (sum, item) => sum + item.total);
}

class CartState {
  final String productId;
  final String productName;
  final String eventSummary;
  /// DailyEntry only — the exact calendar date every hold in this cart was placed for.
  final String? date;
  final List<CartHoldGroup> holds;

  const CartState({
    required this.productId,
    required this.productName,
    required this.eventSummary,
    this.date,
    required this.holds,
  });

  double get grandTotal => holds.fold(0, (sum, h) => sum + h.total);
}

/// Holds the pending purchase intent between an event/museum/parking details
/// screen (which just placed one or more holds via [SectorService.hold])
/// and [PaymentScreen] — a `ValueNotifier`, matching [Session]'s pattern
/// rather than pulling in a state-management package. Deliberately in-memory
/// only (lost on app restart), same tradeoff as `CartService` on web: this
/// is display-only cart data, not auth state.
class Cart {
  Cart._();

  static final ValueNotifier<CartState?> state = ValueNotifier(null);

  static void set(CartState cart) => state.value = cart;

  static void clear() => state.value = null;
}
