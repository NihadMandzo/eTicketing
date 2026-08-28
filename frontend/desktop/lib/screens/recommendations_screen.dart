import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/model_status_response.dart';
import '../providers/api_exception.dart';
import '../providers/recommendation_provider.dart';
import '../theme/app_colors.dart';

/// Back-office view of the recommendation model: what it was trained on, when, and a button to
/// retrain it now rather than waiting for the nightly run.
///
/// It exists because "is the model actually learning" is otherwise unanswerable — the model lives
/// in the Catalog service's memory, loaded from blob storage, and nothing else on the platform
/// surfaces its state. `Preostalo za treniranje` in particular is the number worth watching: it is
/// how many interactions have been recorded since the last run, i.e. how much the model does not
/// yet know.
class RecommendationsScreen extends StatefulWidget {
  const RecommendationsScreen({super.key});

  @override
  State<RecommendationsScreen> createState() => _RecommendationsScreenState();
}

class _RecommendationsScreenState extends State<RecommendationsScreen> {
  final RecommendationProvider _provider = RecommendationProvider();

  ModelStatusResponse? _status;
  bool _isLoading = true;
  bool _isRetraining = false;
  String? _errorMessage;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final status = await _provider.getStatus();
      if (!mounted) return;
      setState(() {
        _status = status;
        _isLoading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.apiError.displayMessage;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'Status modela nije moguće učitati. Pokušajte ponovo.';
        _isLoading = false;
      });
    }
  }

  Future<void> _retrain() async {
    setState(() {
      _isRetraining = true;
      _errorMessage = null;
    });
    try {
      final status = await _provider.retrain();
      if (!mounted) return;
      setState(() {
        _status = status;
        _isRetraining = false;
      });
      _showMessage('Model je uspješno treniran.');
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _isRetraining = false);
      // Includes the 409 "nema zabilježenih interakcija" case — the backend's message already
      // explains it in Bosnian, so it is shown as-is rather than re-worded here.
      _showMessage(e.apiError.displayMessage, isError: true);
    } catch (_) {
      if (!mounted) return;
      setState(() => _isRetraining = false);
      _showMessage('Treniranje nije uspjelo. Pokušajte ponovo.', isError: true);
    }
  }

  void _showMessage(String message, {bool isError = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: isError ? Theme.of(context).colorScheme.error : null,
    ));
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) return const Center(child: CircularProgressIndicator());

    if (_errorMessage != null && _status == null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_errorMessage!, style: TextStyle(color: AppColors.textTertiary(Theme.of(context).brightness))),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
          ],
        ),
      );
    }

    final status = _status!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Header(
            status: status,
            isRetraining: _isRetraining,
            onRetrain: _isRetraining ? null : _retrain,
            onRefresh: _isRetraining ? null : _load,
          ),
          const SizedBox(height: 24),
          _StatGrid(status: status),
          const SizedBox(height: 32),
          Text('Historija treniranja', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 12),
          _HistoryTable(history: status.history, isDark: _isDark),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  final ModelStatusResponse status;
  final bool isRetraining;
  final VoidCallback? onRetrain;
  final VoidCallback? onRefresh;

  const _Header({
    required this.status,
    required this.isRetraining,
    required this.onRetrain,
    required this.onRefresh,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    // Wrap, not Row: at ~700px the description and the two buttons cannot share a line, and a Row
    // would overflow rather than reflow.
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 16,
      runSpacing: 12,
      children: [
        ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 620),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    status.isTrained ? LucideIcons.circleCheck : LucideIcons.circleAlert,
                    size: 18,
                    color: status.isTrained ? AppColors.primary : AppColors.textTertiary(brightness),
                  ),
                  const SizedBox(width: 8),
                  Flexible(
                    child: Text(
                      status.isTrained ? 'Model je aktivan' : 'Model još nije treniran',
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                'Model uči iz kupovina i pregleda proizvoda. Trenira se automatski svake noći, '
                'a dugmetom ispod možete pokrenuti treniranje odmah.',
                style: TextStyle(color: AppColors.textTertiary(brightness)),
              ),
            ],
          ),
        ),
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            OutlinedButton.icon(
              onPressed: onRefresh,
              icon: const Icon(LucideIcons.refreshCw, size: 16),
              label: const Text('Osvježi'),
            ),
            const SizedBox(width: 12),
            FilledButton.icon(
              onPressed: onRetrain,
              icon: isRetraining
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(LucideIcons.brain, size: 16),
              label: Text(isRetraining ? 'Treniranje...' : 'Ponovo treniraj'),
            ),
          ],
        ),
      ],
    );
  }
}

class _StatGrid extends StatelessWidget {
  final ModelStatusResponse status;

  const _StatGrid({required this.status});

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      _StatCard(
        icon: LucideIcons.clock,
        label: 'Zadnje treniranje',
        value: status.trainedAt == null ? 'Nikad' : _formatDateTime(status.trainedAt!.toLocal()),
        hint: status.trainingDurationMs > 0 ? 'Trajalo ${status.trainingDurationMs} ms' : null,
      ),
      _StatCard(
        icon: LucideIcons.activity,
        label: 'Ukupno interakcija',
        value: '${status.totalInteractions}',
        hint: '${status.viewCount} pregleda · ${status.purchaseCount} kupovina',
      ),
      _StatCard(
        icon: LucideIcons.users,
        label: 'Korisnika u modelu',
        value: '${status.modelUserCount}',
        hint: 'Ukupno zabilježeno: ${status.distinctUsers}',
      ),
      _StatCard(
        icon: LucideIcons.package,
        label: 'Proizvoda u modelu',
        value: '${status.modelProductCount}',
        hint: 'Ukupno zabilježeno: ${status.distinctProducts}',
      ),
      _StatCard(
        icon: LucideIcons.hourglass,
        label: 'Čeka na treniranje',
        value: '${status.pendingInteractions}',
        hint: 'Nove interakcije od zadnjeg treniranja',
      ),
    ];

    // MaxCrossAxisExtent rather than a fixed crossAxisCount, and a fixed mainAxisExtent rather
    // than an aspect ratio — same reasoning as categories_screen.dart's grid: columns appear as
    // the window widens instead of cards stretching, and the card height can never overflow.
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 280,
        mainAxisExtent: 130,
        crossAxisSpacing: 12,
        mainAxisSpacing: 12,
      ),
      itemCount: tiles.length,
      itemBuilder: (context, index) => tiles[index],
    );
  }
}

class _StatCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;
  final String? hint;

  const _StatCard({required this.icon, required this.label, required this.value, this.hint});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface(brightness),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border(brightness)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: AppColors.textTertiary(brightness)),
              const SizedBox(width: 8),
              // Expanded, so a long label ellipsises instead of overflowing the card.
              Expanded(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.textTertiary(brightness)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(value, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800)),
          if (hint != null) ...[
            const SizedBox(height: 4),
            Text(
              hint!,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
            ),
          ],
        ],
      ),
    );
  }
}

class _HistoryTable extends StatelessWidget {
  final List<ModelSnapshotResponse> history;
  final bool isDark;

  const _HistoryTable({required this.history, required this.isDark});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    if (history.isEmpty) {
      return Text(
        'Model još nije treniran nijednom.',
        style: TextStyle(color: AppColors.textTertiary(brightness)),
      );
    }

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface(brightness),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border(brightness)),
      ),
      clipBehavior: Clip.antiAlias,
      // The table has fixed-width columns, so it scrolls inside its own box on a narrow window
      // rather than forcing the whole page sideways.
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Vrijeme')),
            DataColumn(label: Text('Interakcija')),
            DataColumn(label: Text('Korisnika')),
            DataColumn(label: Text('Proizvoda')),
            DataColumn(label: Text('Trajanje')),
            DataColumn(label: Text('Status')),
          ],
          rows: history
              .map((snapshot) => DataRow(cells: [
                    DataCell(Text(_formatDateTime(snapshot.trainedAt.toLocal()))),
                    DataCell(Text('${snapshot.interactionCount}')),
                    DataCell(Text('${snapshot.userCount}')),
                    DataCell(Text('${snapshot.productCount}')),
                    DataCell(Text('${snapshot.trainingDurationMs} ms')),
                    DataCell(snapshot.isActive
                        ? const Text('Aktivan', style: TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700))
                        : Text('Arhiva', style: TextStyle(color: AppColors.textTertiary(brightness)))),
                  ]))
              .toList(),
        ),
      ),
    );
  }
}

String _formatDateTime(DateTime date) {
  final day = date.day.toString().padLeft(2, '0');
  final month = date.month.toString().padLeft(2, '0');
  final hour = date.hour.toString().padLeft(2, '0');
  final minute = date.minute.toString().padLeft(2, '0');
  return '$day.$month.${date.year}. $hour:$minute';
}
