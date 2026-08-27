import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../theme/app_colors.dart';

/// A number the user can either step or type: `−` and `+` for nudging, a text
/// field between them for the times a box office wants 1.250 rather than 125
/// button presses.
///
/// **Why the field has no fixed height.** A first version forced the
/// `TextField` into a `SizedBox(height: boxHeight)` and then tried to compute
/// a `contentPadding` that would centre the digits inside it. That fights
/// `InputDecorator` rather than using it: its own vertical placement math
/// (`_RenderDecoration.performLayout`, if you go looking) centres the field's
/// *natural* height inside whatever box constrains it, folding in the
/// platform's `VisualDensity` along the way — desktop defaults to
/// `VisualDensity.compact`, which nudges that math by a few pixels no
/// externally-precomputed padding accounted for. Forcing a tight height short-
/// circuits that centring instead of using it, which is what kept leaving the
/// digits low no matter how the padding was tuned.
///
/// The fix is to stop overriding it: give the field only a fixed *width*, let
/// it size itself to its natural (smaller) height, and let the surrounding
/// `Row` — whose default `crossAxisAlignment` is already `.center` — centre
/// that against the [boxHeight]-tall stepper buttons the ordinary way a Row
/// centres any two children of different height.
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

  static const style = TextStyle(
    fontSize: fontSize,
    fontWeight: FontWeight.w700,
    fontFeatures: [FontFeature.tabularFigures()],
  );

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final border = isDark
        ? AppColors.darkBorderInput
        : AppColors.lightBorderInput;

    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(9),
      ),
      clipBehavior: Clip.antiAlias,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _StepperButton(icon: LucideIcons.minus, onTap: onDecrement),
          SizedBox(
            width: fieldWidth,
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              keyboardType: TextInputType.number,
              inputFormatters: const [QuantityInputFormatter()],
              textAlign: TextAlign.center,
              textAlignVertical: TextAlignVertical.center,
              style: style,
              decoration: InputDecoration(
                hintText: '0',
                hintStyle: style.copyWith(
                  color: isDark
                      ? AppColors.darkTextDisabled
                      : AppColors.lightTextDisabled,
                ),
                filled: false,
                isCollapsed: true,
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(horizontal: 4),
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
              ? (isDark
                    ? AppColors.darkTextDisabled
                    : AppColors.lightTextDisabled)
              : (isDark
                    ? AppColors.darkTextSecondary
                    : AppColors.lightTextSecondary),
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
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final digits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    final cleaned = digits.replaceFirst(RegExp(r'^0+(?=\d)'), '');
    if (cleaned == newValue.text) return newValue;

    final offset =
        newValue.selection.baseOffset - (newValue.text.length - cleaned.length);
    return TextEditingValue(
      text: cleaned,
      selection: TextSelection.collapsed(
        offset: offset.clamp(0, cleaned.length),
      ),
    );
  }
}
