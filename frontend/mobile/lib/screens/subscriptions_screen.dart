import 'package:flutter/material.dart';

import '../models/responses/subscription_response.dart';
import '../services/api_exception.dart';
import '../services/subscription_service.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import '../widgets/responsive_page.dart';

/// "Moje pretplate" -- the buyer's monthly parking reservations, and the only place they can end one.
///
/// Cancelling is deliberately not immediate: they paid for the current period and keep it, so the
/// row stays Aktivna with a note about when it actually stops. See SubscriptionService.CancelAsync.
class SubscriptionsScreen extends StatefulWidget {
  const SubscriptionsScreen({super.key});

  @override
  State<SubscriptionsScreen> createState() => _SubscriptionsScreenState();
}

class _SubscriptionsScreenState extends State<SubscriptionsScreen> {
  final _service = SubscriptionService();

  List<SubscriptionResponse> _subscriptions = const [];
  bool _isLoading = true;
  String? _error;
  String? _cancellingId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final result = await _service.getMine();
      if (!mounted) return;
      setState(() => _subscriptions = result.items);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.apiError.displayMessage);
    } catch (_) {
      if (mounted) setState(() => _error = 'Učitavanje pretplata nije uspjelo.');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _cancel(SubscriptionResponse subscription) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Otkazivanje pretplate',
      message: 'Pretplata za ${subscription.sectorName} vrijedi do ${subscription.currentPeriodEnd} '
          'i nakon toga se više neće naplaćivati.',
      confirmLabel: 'Otkaži pretplatu',
      cancelLabel: 'Odustani',
    );
    if (confirmed != true) return;

    setState(() => _cancellingId = subscription.id);

    try {
      await _service.cancel(subscription.id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Pretplata će biti otkazana ${subscription.currentPeriodEnd}.'),
          backgroundColor: AppColors.success,
        ),
      );
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.apiError.displayMessage), backgroundColor: AppColors.errorDark),
        );
      }
    } finally {
      if (mounted) setState(() => _cancellingId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Scaffold(
      appBar: AppBar(title: const Text('Moje pretplate')),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _load,
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ResponsivePage(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_isLoading)
                    _Placeholder(text: 'Učitavanje...', color: tertiaryText)
                  else if (_error != null)
                    _Placeholder(text: _error!, color: AppColors.errorDark)
                  else if (_subscriptions.isEmpty)
                    _Placeholder(text: 'Nemate aktivnih pretplata.', color: tertiaryText)
                  else
                    for (final subscription in _subscriptions) ...[
                      _SubscriptionCard(
                        subscription: subscription,
                        isCancelling: _cancellingId == subscription.id,
                        onCancel: () => _cancel(subscription),
                      ),
                      const SizedBox(height: 14),
                    ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Placeholder extends StatelessWidget {
  final String text;
  final Color color;

  const _Placeholder({required this.text, required this.color});

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 48),
        child: Text(text, textAlign: TextAlign.center, style: TextStyle(color: color, fontSize: 13)),
      );
}

class _SubscriptionCard extends StatelessWidget {
  final SubscriptionResponse subscription;
  final bool isCancelling;
  final VoidCallback onCancel;

  const _SubscriptionCard({
    required this.subscription,
    required this.isCancelling,
    required this.onCancel,
  });

  Color _statusColor() => switch (subscription.status) {
        'Active' => AppColors.success,
        'PastDue' => AppColors.errorDark,
        _ => Colors.grey,
      };

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final renewal = subscription.nextRenewalAt;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    subscription.sectorName,
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                  ),
                ),
                const SizedBox(width: 10),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: _statusColor().withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    subscription.statusLabel,
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: _statusColor()),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _InfoRow(
              label: 'Tekući period',
              value: '${subscription.currentPeriodStart} - ${subscription.currentPeriodEnd}',
            ),
            _InfoRow(
              label: 'Cijena po periodu',
              value: '${subscription.pricePerPeriod.toStringAsFixed(0)} KM',
            ),
            if (renewal != null && !subscription.cancelAtPeriodEnd)
              _InfoRow(
                label: 'Sljedeća naplata',
                value: '${renewal.day}.${renewal.month}.${renewal.year}.',
              ),
            if (subscription.cancelAtPeriodEnd && subscription.status == 'Active') ...[
              const SizedBox(height: 8),
              Text(
                'Otkazano — vrijedi do ${subscription.currentPeriodEnd}, nakon toga se ne naplaćuje.',
                style: TextStyle(fontSize: 12, color: tertiaryText),
              ),
            ] else if (subscription.status == 'PastDue') ...[
              const SizedBox(height: 8),
              Text(
                'Posljednja naplata nije uspjela. Pokušavamo ponovo; provjerite karticu kod svoje banke.',
                style: TextStyle(fontSize: 12, color: tertiaryText),
              ),
            ],
            if (subscription.isCancellable) ...[
              const SizedBox(height: 14),
              OutlinedButton(
                onPressed: isCancelling ? null : onCancel,
                child: isCancelling
                    ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Otkaži pretplatu'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;

  const _InfoRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(child: Text(label, style: TextStyle(fontSize: 13, color: tertiaryText))),
          const SizedBox(width: 12),
          Flexible(child: Text(value, textAlign: TextAlign.right, style: const TextStyle(fontSize: 13))),
        ],
      ),
    );
  }
}
