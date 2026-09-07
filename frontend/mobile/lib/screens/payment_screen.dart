import 'package:flutter/material.dart';
// `hide Card`: flutter_stripe exports a Card model that collides with Material's Card widget,
// which this screen uses for its order-summary panels.
import 'package:flutter_stripe/flutter_stripe.dart' hide Card;

import '../core/stripe_bootstrap.dart';
import '../models/requests/create_payment_intent_request.dart';
import '../models/requests/purchase_request.dart';
import '../models/responses/payment_intent_response.dart';
import '../services/api_exception.dart';
import '../services/cart.dart';
import '../services/purchase_service.dart';
import '../theme/app_colors.dart';
import '../theme/system_ui.dart';
import '../theme/theme_controller.dart';
import '../utils/validators.dart';
import '../widgets/labeled_field.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'main_shell.dart';

/// Mockup screen 7 — order summary, per-hold line items, and the card step
/// (validated identically to `PurchaseRequestValidator` when the offline
/// gateway is in play).
///
/// **Card is the only payment method, and there is no method picker.** The
/// provider seam is pinned to cards on the backend too (see
/// StripePaymentGateway's PaymentMethodTypes), because manual capture and the
/// saved-card monthly renewal are both card mechanics. A radio group with one
/// option only asks the buyer to confirm something they were never choosing.
/// Purchases every [CartHoldGroup] in [Cart.state] **sequentially, not in
/// parallel** — they share one card, so an early decline means the rest
/// would fail too. Each successful purchase is immediately dropped from
/// [Cart.state] (see [_submit]), so a failure partway through never
/// resubmits an already-confirmed hold and the buyer can just press "Plati"
/// again to pick up where the run stopped; any hold not yet attempted
/// simply expires via its own 5-min TTL if abandoned instead of retried
/// (same as `frontend/web`'s CheckoutComponent).
class PaymentScreen extends StatefulWidget {
  const PaymentScreen({super.key});

  @override
  State<PaymentScreen> createState() => _PaymentScreenState();
}

class _PaymentScreenState extends State<PaymentScreen> {
  final _formKey = GlobalKey<FormState>();
  final _purchaseService = PurchaseService();

  final _cardNumberCtrl = TextEditingController();
  final _expiryCtrl = TextEditingController();
  final _cvvCtrl = TextEditingController();

  bool _isSubmitting = false;
  bool _isPreparing = true;
  String? _submitError;

  /// The payment object for the hold currently being paid for. Null until [_prepare] answers, which
  /// is also what tells this screen which provider is configured.
  PaymentIntentResponse? _intent;

  @override
  void initState() {
    super.initState();
    // Fetched up front rather than on submit so the correct payment UI is rendered from the start,
    // instead of flashing the mock card form and then replacing it.
    WidgetsBinding.instance.addPostFrameCallback((_) => _prepare());
  }

  @override
  void dispose() {
    _cardNumberCtrl.dispose();
    _expiryCtrl.dispose();
    _cvvCtrl.dispose();
    super.dispose();
  }

  /// Asks the backend to price the current hold and create the payment object for it. Also tells
  /// this screen which provider is configured, which is what decides between Stripe's payment sheet
  /// and the offline mock card form below.
  Future<void> _prepare() async {
    final cart = Cart.state.value;
    if (cart == null || cart.holds.isEmpty) return;

    setState(() {
      _isPreparing = true;
      _submitError = null;
    });

    try {
      final hold = cart.holds.first;
      final intent = await _purchaseService.createPaymentIntent(CreatePaymentIntentRequest(
        holdId: hold.holdId,
        lineItems: hold.lineItems
            .map((item) => PurchaseLineItemRequest(ticketTypeId: item.ticketTypeId, quantity: item.quantity))
            .toList(),
      ));
      if (!mounted) return;
      setState(() => _intent = intent);
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false,
        );
        return;
      }
      setState(() => _submitError = e.apiError.displayMessage);
    } catch (_) {
      if (mounted) setState(() => _submitError = 'Priprema plaćanja nije uspjela. Pokušajte ponovo.');
    } finally {
      if (mounted) setState(() => _isPreparing = false);
    }
  }

  Future<void> _submit() async {
    final cart = Cart.state.value;
    if (cart == null || cart.holds.isEmpty) return;

    final intent = _intent;
    if (intent == null || _isSubmitting) return;

    if (intent.isMock && !_formKey.currentState!.validate()) return;

    setState(() {
      _isSubmitting = true;
      _submitError = null;
    });

    final totalHolds = cart.holds.length;
    var remaining = List<CartHoldGroup>.from(cart.holds);
    var succeededCount = 0;
    var current = intent;

    try {
      while (remaining.isNotEmpty) {
        final hold = remaining.first;

        if (!current.isMock) {
          final authorized = await _payWithStripe(current);
          if (!authorized) return;
        }

        await _purchaseService.purchase(PurchaseRequest(
          holdId: hold.holdId,
          lineItems: hold.lineItems
              .map((item) => PurchaseLineItemRequest(ticketTypeId: item.ticketTypeId, quantity: item.quantity))
              .toList(),
          orderId: current.orderId,
          paymentIntentId: current.intentId,
          // Only the offline gateway reads this; with Stripe the card never reaches this app.
          simulatedLast4: current.isMock ? _simulatedLast4() : null,
        ));

        succeededCount++;
        remaining = remaining.sublist(1);
        // Drop the just-purchased hold immediately, so a failure partway through a multi-sector
        // basket never resubmits one that is already paid for.
        Cart.set(CartState(
          productId: cart.productId,
          productName: cart.productName,
          eventSummary: cart.eventSummary,
          date: cart.date,
          holds: remaining,
        ));

        if (remaining.isEmpty) break;

        // Each hold is its own order and its own payment: with manual capture that keeps every
        // authorization independently cancellable, which one basket-wide charge could not be.
        final next = await _purchaseService.createPaymentIntent(CreatePaymentIntentRequest(
          holdId: remaining.first.holdId,
          lineItems: remaining.first.lineItems
              .map((item) => PurchaseLineItemRequest(ticketTypeId: item.ticketTypeId, quantity: item.quantity))
              .toList(),
        ));
        current = next;
        if (mounted) setState(() => _intent = next);
      }

      Cart.clear();
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const MainShell(initialIndex: 1)),
        (route) => false,
      );
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Ulaznice su uspješno kupljene!'), backgroundColor: AppColors.success),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false,
        );
        return;
      }
      // payment.declined / purchase.hold_expired / purchase.quantity_mismatch -> 400,
      // client-fixable, shown inline. payment.unavailable -> 503, a genuine outage, shown as a
      // toast instead (see PurchaseService.PurchaseAsync's 400-vs-503 distinction).
      final message = _buildFailureMessage(e.apiError.displayMessage, succeededCount, totalHolds);
      if (e.statusCode == 503) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message), backgroundColor: AppColors.errorDark),
        );
      } else {
        setState(() => _submitError = message);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _submitError =
            _buildFailureMessage('Plaćanje nije uspjelo. Pokušajte ponovo.', succeededCount, totalHolds));
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  /// Opens Stripe's native payment sheet, which handles 3-D Secure itself.
  ///
  /// Returns false when the buyer backed out or the card was declined — in both cases the hold is
  /// deliberately left alive so they can simply try again with another card.
  Future<bool> _payWithStripe(PaymentIntentResponse intent) async {
    final key = intent.publishableKey;
    final secret = intent.clientSecret;
    if (key == null || secret == null) {
      setState(() => _submitError = 'Plaćanje nije spremno. Pokušajte ponovo.');
      return false;
    }

    await StripeBootstrap.ensureInitialized(key);

    await Stripe.instance.initPaymentSheet(
      paymentSheetParameters: SetupPaymentSheetParameters(
        paymentIntentClientSecret: secret,
        merchantDisplayName: 'eKarta',
        // Follows the app's own dark-mode toggle rather than the OS, matching every other screen.
        style: ThemeController.mode.value == ThemeMode.dark ? ThemeMode.dark : ThemeMode.light,
      ),
    );

    try {
      await Stripe.instance.presentPaymentSheet();
      return true;
    } on StripeException catch (e) {
      // Backing out of the sheet is not an error and must not show one.
      if (e.error.code == FailureCode.Canceled) return false;

      setState(() => _submitError = e.error.localizedMessage ?? 'Plaćanje nije uspjelo. Pokušajte ponovo.');
      return false;
    }
  }

  /// Last four digits of whatever was typed into the mock card field. Only the offline gateway
  /// reads it, and only to decide whether to simulate a decline ("0000").
  String _simulatedLast4() {
    final digits = _cardNumberCtrl.text.replaceAll(RegExp(r'\s'), '');
    return digits.length <= 4 ? digits : digits.substring(digits.length - 4);
  }

  String _buildFailureMessage(String base, int succeededCount, int totalHolds) {
    if (succeededCount == 0) return base;
    return '$base ($succeededCount od $totalHolds narudžbi je uspješno obrađeno. Preostale možete pokušati ponovo.)';
  }

  @override
  Widget build(BuildContext context) {
    final cart = Cart.state.value;
    if (cart == null || cart.holds.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: const Text('Plaćanje')),
        body: const Center(child: Text('Korpa je prazna.')),
      );
    }

    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return Scaffold(
      appBar: AppBar(title: const Text('Plaćanje')),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: SingleChildScrollView(
                child: ResponsivePage(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Row(
                              children: [
                                Container(
                                  width: 52,
                                  height: 52,
                                  decoration: BoxDecoration(
                                    borderRadius: BorderRadius.circular(10),
                                    gradient: const LinearGradient(colors: [AppColors.primary, AppColors.secondary]),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(cart.productName, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                                      if (cart.eventSummary.isNotEmpty)
                                        Text(cart.eventSummary, style: TextStyle(fontSize: 12, color: tertiaryText)),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 18),
                        const Text('Pregled narudžbe', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 10),
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              children: [
                                for (final hold in cart.holds)
                                  for (final item in hold.lineItems)
                                    Padding(
                                      padding: const EdgeInsets.only(bottom: 10),
                                      child: Row(
                                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                        children: [
                                          Text(
                                            '${item.ticketTypeName ?? hold.sectorName} × ${item.quantity}',
                                            style: TextStyle(fontSize: 13, color: tertiaryText),
                                          ),
                                          Text('${item.total.toStringAsFixed(0)} KM', style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
                                        ],
                                      ),
                                    ),
                                const Divider(height: 1),
                                const SizedBox(height: 10),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    const Text('Ukupno za plaćanje', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700)),
                                    Text(
                                      '${cart.grandTotal.toStringAsFixed(0)} KM',
                                      style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Theme.of(context).colorScheme.primary),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 18),
                        const Text('Plaćanje karticom', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 10),
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: _isPreparing
                                ? Row(
                                    children: [
                                      const SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(strokeWidth: 2),
                                      ),
                                      const SizedBox(width: 12),
                                      Text('Pripremamo plaćanje...',
                                          style: TextStyle(fontSize: 13, color: tertiaryText)),
                                    ],
                                  )
                                : (_intent?.isMock ?? false)
                                    ? Column(
                                        crossAxisAlignment: CrossAxisAlignment.stretch,
                                        children: [
                                          // PAYMENT_PROVIDER=Mock: the offline gateway, so there is
                                          // no payment SDK and the card is typed here. Kept working
                                          // on purpose -- it is the documented fallback
                                          // (arhitektura-migracija-mikroservisi-eda.md s9).
                                          LabeledField(
                                            label: 'Broj kartice',
                                            controller: _cardNumberCtrl,
                                            hintText: '4242 4242 4242 4242',
                                            keyboardType: TextInputType.number,
                                            validator: Validators.cardNumber,
                                          ),
                                          const SizedBox(height: 16),
                                          Row(
                                            crossAxisAlignment: CrossAxisAlignment.start,
                                            children: [
                                              Expanded(
                                                child: LabeledField(
                                                  label: 'Datum isteka',
                                                  controller: _expiryCtrl,
                                                  hintText: 'MM/GG',
                                                  keyboardType: TextInputType.number,
                                                  validator: Validators.cardExpiry,
                                                ),
                                              ),
                                              const SizedBox(width: 14),
                                              Expanded(
                                                child: LabeledField(
                                                  label: 'CVV',
                                                  controller: _cvvCtrl,
                                                  hintText: '123',
                                                  obscureText: true,
                                                  keyboardType: TextInputType.number,
                                                  validator: Validators.cardCvv,
                                                ),
                                              ),
                                            ],
                                          ),
                                          const SizedBox(height: 10),
                                          Text(
                                            'Testni način rada. Prolazi svaki broj koji ne završava na 0000.',
                                            style: TextStyle(fontSize: 12, color: tertiaryText),
                                          ),
                                        ],
                                      )
                                    // Stripe collects the card in its own native payment sheet,
                                    // which opens when "Plati" is pressed. Nothing card-shaped
                                    // exists in this app.
                                    : Row(
                                        crossAxisAlignment: CrossAxisAlignment.start,
                                        children: [
                                          Icon(Icons.credit_card_rounded, size: 20, color: primary),
                                          const SizedBox(width: 12),
                                          Expanded(
                                            child: Column(
                                              crossAxisAlignment: CrossAxisAlignment.start,
                                              children: [
                                                const Text('Platna kartica',
                                                    style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                                                const SizedBox(height: 4),
                                                Text(
                                                  'Broj kartice unosite u Stripe formi koja se otvara kad potvrdite plaćanje.',
                                                  style: TextStyle(fontSize: 13, height: 1.4, color: tertiaryText),
                                                ),
                                              ],
                                            ),
                                          ),
                                        ],
                                      ),
                          ),
                        ),
                        if (_submitError != null) ...[
                          const SizedBox(height: 16),
                          Text(_submitError!, style: const TextStyle(color: AppColors.errorDark, fontSize: 13)),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
            // One bar, one action. Previously this was a bordered box wrapping a white strip
            // wrapping a stadium-shaped pill — three nested shapes for a single button. The bar is
            // now the surface, the button fills it, and its corner radius matches the cards above
            // it instead of introducing a fourth shape language.
            SystemBarBackdrop(
              child: Container(
                padding: const EdgeInsets.fromLTRB(20, 14, 20, 14),
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurface : AppColors.lightSurface,
                  border: Border(top: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder)),
                ),
                child: SizedBox(
                  height: 52,
                  child: FilledButton(
                    onPressed: (_isSubmitting || _isPreparing || _intent == null) ? null : _submit,
                    child: _isSubmitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : Text(
                            'Plati ${cart.grandTotal.toStringAsFixed(0)} KM',
                            style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
