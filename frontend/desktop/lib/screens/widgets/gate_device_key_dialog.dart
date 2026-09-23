import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/api_config.dart';
import '../../theme/app_colors.dart';
import '../../utility/snackbar_service.dart';

/// Shown once, immediately after a gate device is registered or its key rotated.
///
/// This is the only moment the plaintext key exists anywhere outside the device — the server keeps
/// nothing but a SHA-256 hash. Hence the deliberate friction: no barrier dismiss, an explicit
/// warning, and a one-click copy of the exact `config.h` block the firmware wants, so nobody is
/// tempted to "write it down later".
class GateDeviceKeyDialog extends StatelessWidget {
  final String deviceName;
  final String apiKey;

  /// True when this is a rotation rather than a first registration — the wording changes, because
  /// rotating silently bricks the device until someone re-flashes it.
  final bool isRotation;

  const GateDeviceKeyDialog({
    super.key,
    required this.deviceName,
    required this.apiKey,
    this.isRotation = false,
  });

  static Future<void> show(
    BuildContext context, {
    required String deviceName,
    required String apiKey,
    bool isRotation = false,
  }) =>
      showDialog(
        context: context,
        // The key is unrecoverable — a stray tap outside the dialog must not be able to lose it.
        barrierDismissible: false,
        builder: (_) => GateDeviceKeyDialog(
          deviceName: deviceName,
          apiKey: apiKey,
          isRotation: isRotation,
        ),
      );

  /// The firmware wants a base URL with no trailing slash. ApiConfig.baseUrl carries one.
  String get _apiBase {
    final base = ApiConfig.baseUrl;
    return base.endsWith('/') ? base.substring(0, base.length - 1) : base;
  }

  String get _configSnippet => '#define GATE_API_BASE_URL "$_apiBase"\n'
      '#define GATE_DEVICE_KEY    "$apiKey"';

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return AlertDialog(
      backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      title: Row(
        children: [
          Icon(LucideIcons.keyRound, color: primary, size: 22),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              isRotation ? 'Novi ključ uređaja' : 'Ključ uređaja',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              ),
            ),
          ),
        ],
      ),
      content: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                deviceName,
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                ),
              ),
              const SizedBox(height: 14),
              _WarningBanner(
                isDark: isDark,
                text: isRotation
                    ? 'Ključ se prikazuje samo jednom. Stari ključ više ne važi — uređaj neće '
                        'raditi dok mu ne upišete novi ključ i ponovo ga ne programirate.'
                    : 'Ključ se prikazuje samo jednom. Sačuvajte ga sada — ako ga izgubite, '
                        'možete generisati novi, ali stari se ne može vratiti.',
              ),
              const SizedBox(height: 18),
              _CopyBlock(
                isDark: isDark,
                label: 'API ključ',
                value: apiKey,
                onCopy: () => _copy(apiKey, 'Ključ je kopiran.'),
              ),
              const SizedBox(height: 14),
              _CopyBlock(
                isDark: isDark,
                label: 'Konfiguracija za uređaj (IoT/include/config.h)',
                value: _configSnippet,
                onCopy: () => _copy(_configSnippet, 'Konfiguracija je kopirana.'),
              ),
              const SizedBox(height: 12),
              Text(
                'Ako je adresa iznad "localhost", zamijenite je IP adresom ovog računara na '
                'mreži (npr. 192.168.1.10) — uređaj je zaseban uređaj i ne može doći do "localhost". '
                'Adresu sa http:// prihvata samo razvojna verzija firmvera (esp32cam-dev), i to samo '
                'na lokalnoj mreži. Stvarni uređaj (esp32cam) traži https:// adresu i korijenski '
                'certifikat servera (GATE_API_ROOT_CA) — vidi IoT/README.md.',
                style: TextStyle(
                  fontSize: 12,
                  height: 1.5,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        FilledButton(
          style: FilledButton.styleFrom(backgroundColor: primary),
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Sačuvao/la sam ključ'),
        ),
      ],
    );
  }

  void _copy(String value, String message) {
    Clipboard.setData(ClipboardData(text: value));
    SnackbarService.showSuccess(message);
  }
}

class _WarningBanner extends StatelessWidget {
  final bool isDark;
  final String text;

  const _WarningBanner({required this.isDark, required this.text});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: isDark ? AppColors.warningDark.withValues(alpha: 0.15) : const Color(0xFFFFFBEB),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.5)),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(LucideIcons.triangleAlert, color: AppColors.warningDark, size: 18),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: TextStyle(
                fontSize: 13,
                height: 1.5,
                color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CopyBlock extends StatelessWidget {
  final bool isDark;
  final String label;
  final String value;
  final VoidCallback onCopy;

  const _CopyBlock({
    required this.isDark,
    required this.label,
    required this.value,
    required this.onCopy,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                label,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                ),
              ),
            ),
            TextButton.icon(
              onPressed: onCopy,
              icon: const Icon(LucideIcons.copy, size: 15),
              label: const Text('Kopiraj'),
              style: TextButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding: const EdgeInsets.symmetric(horizontal: 8),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkInputFill : AppColors.lightSurfaceMuted,
            border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
            borderRadius: BorderRadius.circular(10),
          ),
          child: SelectableText(
            value,
            style: TextStyle(
              fontFamily: 'monospace',
              fontSize: 12.5,
              height: 1.6,
              color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
            ),
          ),
        ),
      ],
    );
  }
}
