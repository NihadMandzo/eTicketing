import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../theme/app_colors.dart';

/// A number the user can either step or type: `−` and `+` for nudging, a text
/// field between them for the times a box office wants 1.250 rather than 125
/// button presses.
///
/// The three parts share one [boxHeight] and the digits sit on the same optical
/// axis as the two icons — see [_fieldPadding] for why that takes an explicit
/// padding rather than a `Center`.
class QuantityStepperField extends StatelessWidget {
  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  /// Null disables the corresponding button — at zero, and at the ceiling.
  final VoidCallback? onDecrement;
  final VoidCallback? onIncrement;

  const QuantityStepperField({
    super.key,
    required this.controller,
    required this.onChanged,
    this.onDecrement,
    this.onIncrement,
  });

  static const boxHeight = 34.0;
  static const fieldWidth = 62.0;
  static const fontSize = 14.0;

  /// Centring the digits is done by padding, not by wrapping the field in a
  /// `Center` or leaning on `textAlignVertical`.
  ///
  /// A text line is not vertically symmetric: it reserves descender room below
  /// the baseline that digits never use, so centring the *line box* leaves the
  /// digits sitting low. This padding centres the line box and then lifts it by
  /// half the descent, which puts the visual mass of the digits on the box's
  /// centre line — the same place the 14px stepper icons sit.
  ///
  /// `quantity_stepper_field_test.dart` measures this against the real caret
  /// rect, so if a font or a size changes the arithmetic, the test says so.
  static EdgeInsets _fieldPadding(TextPainter probe) {
    final line = probe.height;
    final descent = line - probe.computeDistanceToActualBaseline(TextBaseline.alphabetic);
    final free = boxHeight - line;

    return EdgeInsets.only(
      top: (free / 2 - descent / 2).clamp(0.0, free),
      bottom: (free / 2 + descent / 2).clamp(0.0, free),
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final border = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;

    const style = TextStyle(
      fontSize: fontSize,
      fontWeight: FontWeight.w700,
      fontFeatures: [FontFeature.tabularFigures()],
    );

    final probe = TextPainter(
      text: const TextSpan(text: '0', style: style),
      textDirection: TextDirection.ltr,
    )..layout();
    final padding = _fieldPadding(probe);
    probe.dispose();

    return Container(
      decoration: BoxDecoration(border: Border.all(color: border), borderRadius: BorderRadius.circular(9)),
      clipBehavior: Clip.antiAlias,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _StepperButton(icon: LucideIcons.minus, onTap: onDecrement),
          SizedBox(
            width: fieldWidth,
            height: boxHeight,
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              keyboardType: TextInputType.number,
              inputFormatters: const [QuantityInputFormatter()],
              textAlign: TextAlign.center,
              style: style,
              decoration: InputDecoration(
                hintText: '0',
                hintStyle: style.copyWith(
                  color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled,
                ),
                filled: false,
                isCollapsed: true,
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                contentPadding: padding + const EdgeInsets.symmetric(horizontal: 4),
              ),
            ),
          ),
          _StepperButton(icon: LucideIcons.plus, onTap: onIncrement),
        ],
      ),
    );
  }
}

class _StepperButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _StepperButton({required this.icon, this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return InkWell(
      onTap: onTap,
      child: SizedBox(
        width: 30,
        height: QuantityStepperField.boxHeight,
        child: Icon(
          icon,
          size: 14,
          color: onTap == null
              ? (isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled)
              : (isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
        ),
      ),
    );
  }
}

/// Digits only, and never a leading zero: "007" is not a quantity anyone means
/// to type, and letting it stand leaves the field disagreeing with the total.
class QuantityInputFormatter extends TextInputFormatter {
  const QuantityInputFormatter();

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final digits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    final cleaned = digits.replaceFirst(RegExp(r'^0+(?=\d)'), '');
    if (cleaned == newValue.text) return newValue;

    final offset = newValue.selection.baseOffset - (newValue.text.length - cleaned.length);
    return TextEditingValue(
      text: cleaned,
      selection: TextSelection.collapsed(offset: offset.clamp(0, cleaned.length)),
    );
  }
}
