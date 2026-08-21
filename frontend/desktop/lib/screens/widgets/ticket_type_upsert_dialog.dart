import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/requests/ticket_type_upsert_request.dart';
import '../../models/responses/ticket_type_response.dart';
import '../../providers/ticket_type_provider.dart';
import '../../theme/app_colors.dart';

/// Compact create/edit dialog for one TicketType (e.g. "Dijete"/"Student"/
/// "Odrasli") under a Sector — TicketType has no Draft/Published lifecycle
/// of its own, so unlike SectorUpsertDialog there is no preview step, just
/// Name + Price.
class TicketTypeUpsertDialog extends StatefulWidget {
  final String sectorId;
  final TicketTypeResponse? ticketType;
  final VoidCallback onSaved;

  const TicketTypeUpsertDialog({super.key, required this.sectorId, this.ticketType, required this.onSaved});

  @override
  State<TicketTypeUpsertDialog> createState() => _TicketTypeUpsertDialogState();
}

class _TicketTypeUpsertDialogState extends State<TicketTypeUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _priceCtrl = TextEditingController();
  late final _provider = TicketTypeProvider(widget.sectorId);

  bool _isSaving = false;
  bool get _isEditing => widget.ticketType != null;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;
  Color get _primary => _isDark ? AppColors.secondary : AppColors.primary;

  @override
  void initState() {
    super.initState();
    if (_isEditing) {
      _nameCtrl.text = widget.ticketType!.name;
      _priceCtrl.text = widget.ticketType!.price.toString();
    }
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _priceCtrl.dispose();
    super.dispose();
  }

  Future<void> _doSave() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    final request = TicketTypeUpsertRequest(name: _nameCtrl.text.trim(), price: double.parse(_priceCtrl.text.trim()));
    try {
      if (_isEditing) {
        await _provider.updateTicketType(widget.ticketType!.id, request);
      } else {
        await _provider.createTicketType(request);
      }
      if (mounted) {
        Navigator.of(context).pop();
        widget.onSaved();
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        handleApiError(e);
      }
    }
  }

  InputDecoration _inputDecoration(String hint) {
    final placeholderColor = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextDisabled;
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: _primary, width: 2)),
      errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.error)),
      filled: true,
      fillColor: _isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textSecondary = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;
    final onPrimaryColor = isDark ? AppColors.darkBackground : Colors.white;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 380),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                    color: _primary,
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            _isEditing ? 'Uredi Vrstu Ulaznice' : 'Nova Vrsta Ulaznice',
                            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: Colors.white),
                          ),
                        ),
                        InkWell(
                          borderRadius: BorderRadius.circular(8),
                          onTap: () => Navigator.of(context).pop(),
                          child: const Padding(padding: EdgeInsets.all(6), child: Icon(LucideIcons.x, color: Colors.white, size: 18)),
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text('Naziv *', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: textSecondary)),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _nameCtrl,
                          decoration: _inputDecoration('npr. Dijete, Student, Odrasli'),
                          style: TextStyle(fontSize: 14, color: textPrimary),
                          validator: (v) {
                            if (v == null || v.trim().isEmpty) return 'Naziv je obavezan';
                            if (v.trim().length > 100) return 'Naziv može imati maksimalno 100 karaktera';
                            return null;
                          },
                        ),
                        const SizedBox(height: 14),
                        Text('Cijena (KM) *', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: textSecondary)),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _priceCtrl,
                          keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          decoration: _inputDecoration('npr. 4'),
                          style: TextStyle(fontSize: 14, color: textPrimary),
                          validator: (v) {
                            final n = double.tryParse(v?.trim() ?? '');
                            if (n == null || n <= 0) return 'Cijena mora biti veća od 0';
                            return null;
                          },
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        OutlinedButton(
                          onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: textSecondary,
                            side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
                          ),
                          child: const Text('Otkaži', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 13)),
                        ),
                        const SizedBox(width: 10),
                        FilledButton(
                          onPressed: _isSaving ? null : _doSave,
                          style: FilledButton.styleFrom(
                            backgroundColor: _primary,
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                            elevation: 0,
                          ),
                          child: _isSaving
                              ? SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: onPrimaryColor))
                              : Text('Sačuvaj', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13, color: onPrimaryColor)),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
