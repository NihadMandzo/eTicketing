import 'package:flutter_stripe/flutter_stripe.dart';

/// Initialises the Stripe SDK the first time a publishable key arrives.
///
/// The key comes from the server on the payment-intent response rather than a `--dart-define`, so
/// rotating it or switching providers needs no rebuild of this app — which also means it is not
/// known at startup and this cannot live in main().
class StripeBootstrap {
  StripeBootstrap._();

  static String? _appliedKey;

  /// Idempotent: applying the same key twice is a no-op, so this can be called before every payment
  /// without paying for a settings round trip each time.
  static Future<void> ensureInitialized(String publishableKey) async {
    if (_appliedKey == publishableKey) return;

    Stripe.publishableKey = publishableKey;
    await Stripe.instance.applySettings();
    _appliedKey = publishableKey;
  }
}
