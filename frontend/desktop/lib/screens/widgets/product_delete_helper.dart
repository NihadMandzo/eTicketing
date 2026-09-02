import 'package:flutter/material.dart';

import '../../models/responses/product_response.dart';
import '../../providers/product_provider.dart';
import '../../widgets/confirm_dialog.dart';
import '../../main.dart';

/// Shared confirm → delete → handleApiError sequence for deleting a [ProductResponse], used by
/// both `ProductsScreen`'s grid and `ProductDetailScreen`'s own delete action — the two screens
/// previously duplicated this exact sequence, differing only in what happens after a successful
/// delete (adjust pagination vs. pop back to the list). Callers handle that follow-up themselves,
/// gated on the returned bool.
///
/// Returns `true` only if the product was actually deleted. Returns `false` if the user cancelled
/// the confirmation, or if the delete call failed (the error has already been reported via
/// [handleApiError] in that case — callers don't need to handle it again).
Future<bool> confirmAndDeleteProduct(BuildContext context, ProductResponse product) async {
  final confirmed = await ConfirmDialog.show(
    context,
    title: 'Obriši proizvod',
    message: 'Da li ste sigurni da želite obrisati proizvod "${product.name}"?',
    confirmLabel: 'Obriši',
  );
  if (confirmed != true || !context.mounted) return false;

  try {
    await ProductProvider().delete(product.id);
    handleApiSuccess('Proizvod "${product.name}" je obrisan.');
    return true;
  } catch (e) {
    if (context.mounted) handleApiError(e);
    return false;
  }
}
