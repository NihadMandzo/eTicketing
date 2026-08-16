import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// A text field with its label rendered *above* the input as a small bold
/// caption, rather than Material's floating `labelText` inside the border —
/// matches the design mockup referenced in the mobile frontend rules
/// (label-above-field, filled 12px-radius input). Reuse this for every new
/// form field instead of `TextFormField` + `labelText` directly, so
/// login/register/profile forms stay visually consistent.
class LabeledField extends StatelessWidget {
  final String label;
  final TextEditingController controller;
  final String? hintText;
  final String? errorText;
  final bool obscureText;
  final TextInputType? keyboardType;
  final Widget? prefixIcon;
  final Widget? suffixIcon;
  final String? Function(String?)? validator;
  final TextCapitalization textCapitalization;
  final int? maxLength;
  final TextAlign textAlign;
  final TextStyle? style;

  const LabeledField({
    super.key,
    required this.label,
    required this.controller,
    this.hintText,
    this.errorText,
    this.obscureText = false,
    this.keyboardType,
    this.prefixIcon,
    this.suffixIcon,
    this.validator,
    this.textCapitalization = TextCapitalization.none,
    this.maxLength,
    this.textAlign = TextAlign.start,
    this.style,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
          ),
        ),
        const SizedBox(height: 6),
        TextFormField(
          controller: controller,
          obscureText: obscureText,
          keyboardType: keyboardType,
          textCapitalization: textCapitalization,
          maxLength: maxLength,
          textAlign: textAlign,
          style: style,
          validator: validator,
          decoration: InputDecoration(
            hintText: hintText,
            errorText: errorText,
            prefixIcon: prefixIcon,
            suffixIcon: suffixIcon,
            counterText: '',
          ),
        ),
      ],
    );
  }
}

/// [LabeledField] variant for passwords — adds a "Prikaži"/"Sakrij" text
/// toggle in place of an eye icon, matching the mockup's login screen.
class LabeledPasswordField extends StatefulWidget {
  final String label;
  final TextEditingController controller;
  final String? hintText;
  final String? errorText;
  final String? Function(String?)? validator;

  const LabeledPasswordField({
    super.key,
    required this.label,
    required this.controller,
    this.hintText,
    this.errorText,
    this.validator,
  });

  @override
  State<LabeledPasswordField> createState() => _LabeledPasswordFieldState();
}

class _LabeledPasswordFieldState extends State<LabeledPasswordField> {
  bool _obscure = true;

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    return LabeledField(
      label: widget.label,
      controller: widget.controller,
      hintText: widget.hintText,
      errorText: widget.errorText,
      obscureText: _obscure,
      validator: widget.validator,
      suffixIcon: TextButton(
        style: TextButton.styleFrom(foregroundColor: primary),
        onPressed: () => setState(() => _obscure = !_obscure),
        child: Text(_obscure ? 'Prikaži' : 'Sakrij', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
      ),
    );
  }
}
