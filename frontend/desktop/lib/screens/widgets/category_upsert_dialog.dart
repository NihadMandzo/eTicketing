import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../main.dart';
import '../../models/requests/category_insert_request.dart';
import '../../models/requests/category_update_request.dart';
import '../../models/responses/category_response.dart';
import '../../providers/category_provider.dart';
import '../../theme/app_colors.dart';

class CategoryUpsertDialog extends StatefulWidget {
  final CategoryResponse? category;
  final VoidCallback onSaved;

  const CategoryUpsertDialog({
    super.key,
    this.category,
    required this.onSaved,
  });

  @override
  State<CategoryUpsertDialog> createState() => _CategoryUpsertDialogState();
}

class _CategoryUpsertDialogState extends State<CategoryUpsertDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _descController = TextEditingController();
  final _provider = CategoryProvider();

  File? _iconFile;
  String? _iconFileName;
  bool _isSaving = false;

  bool get _isEditing => widget.category != null;
  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    if (_isEditing) {
      _nameController.text = widget.category!.name;
      _descController.text = widget.category!.description;
    }
  }

  @override
  void dispose() {
    _nameController.dispose();
    _descController.dispose();
    super.dispose();
  }

  Future<void> _pickIcon() async {
    // PNG only, matching the backend validator (CategoryIconValidation) —
    // ≤100x100px, ≤100KB, enforced server-side regardless of what's picked
    // here.
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['png'],
      allowMultiple: false,
    );
    if (result != null && result.files.single.path != null) {
      setState(() {
        _iconFile = File(result.files.single.path!);
        _iconFileName = result.files.single.name;
      });
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    if (!_isEditing && _iconFile == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Row(
            children: [
              Icon(Icons.error_outline, color: Colors.white),
              SizedBox(width: 10),
              Text('Ikona kategorije je obavezna'),
            ],
          ),
          backgroundColor: AppColors.errorDark,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          margin: const EdgeInsets.all(16),
        ),
      );
      return;
    }

    setState(() => _isSaving = true);

    try {
      if (_isEditing) {
        await _provider.updateCategory(
          widget.category!.id,
          CategoryUpdateRequest(
            name: _nameController.text.trim(),
            description: _descController.text.trim(),
          ),
          iconFile: _iconFile,
        );
      } else {
        await _provider.insertCategory(
          CategoryInsertRequest(
            name: _nameController.text.trim(),
            description: _descController.text.trim(),
          ),
          iconFile: _iconFile!,
        );
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
    final isDark = _isDark;
    final placeholderColor =
        isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled;
    final borderColor =
        isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    return InputDecoration(
      hintText: hint,
      hintStyle: TextStyle(color: placeholderColor, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(color: borderColor),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(color: borderColor),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(color: primary, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: AppColors.error),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: AppColors.error, width: 2),
      ),
      filled: true,
      fillColor: isDark ? AppColors.darkInputFill : Colors.white,
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;
    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // ── Header ──────────────────────────────────────────
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      colors: [primary, primaryDark],
                    ),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _isEditing ? 'Uredi Kategoriju' : 'Kreiraj Novu Kategoriju',
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                            color: Colors.white,
                          ),
                        ),
                      ),
                      InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: () => Navigator.of(context).pop(),
                        hoverColor: Colors.white12,
                        child: const Padding(
                          padding: EdgeInsets.all(6),
                          child: Icon(LucideIcons.x, color: Colors.white, size: 18),
                        ),
                      ),
                    ],
                  ),
                ),

                // ── Form ────────────────────────────────────────────
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        // Name
                        _Label('Naziv Kategorije *'),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _nameController,
                          decoration: _inputDecoration('npr. Muzika, Sport, Tehnologija'),
                          style: const TextStyle(fontSize: 14),
                          validator: (v) {
                            if (v == null || v.trim().isEmpty) {
                              return 'Naziv kategorije je obavezan';
                            }
                            if (v.trim().length < 2) {
                              return 'Naziv mora imati najmanje 2 karaktera';
                            }
                            if (v.trim().length > 100) {
                              return 'Naziv može imati maksimalno 100 karaktera';
                            }
                            return null;
                          },
                        ),

                        const SizedBox(height: 14),

                        // Description
                        _Label('Opis Kategorije'),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _descController,
                          maxLines: 3,
                          decoration: _inputDecoration('Unesite kratak opis kategorije...'),
                          style: const TextStyle(fontSize: 14),
                          validator: (v) {
                            if (v != null && v.trim().length > 500) {
                              return 'Opis može imati maksimalno 500 karaktera';
                            }
                            return null;
                          },
                        ),

                        const SizedBox(height: 14),

                        // Icon picker
                        _Label(
                          _isEditing
                              ? 'Ikona (ostavite prazno da zadržite postojeću)'
                              : 'Ikona Kategorije *',
                        ),
                        const SizedBox(height: 6),
                        _IconPickerTile(
                          iconFile: _iconFile,
                          fileName: _iconFileName,
                          onTap: _pickIcon,
                        ),
                      ],
                    ),
                  ),
                ),

                // ── Footer ──────────────────────────────────────────
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      OutlinedButton(
                        onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: isDark
                              ? AppColors.darkTextSecondary
                              : AppColors.lightTextSecondary,
                          side: BorderSide(
                              color: isDark
                                  ? AppColors.darkBorderInput
                                  : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 18, vertical: 10),
                        ),
                        child: const Text('Otkaži',
                            style: TextStyle(
                                fontWeight: FontWeight.w500, fontSize: 13)),
                      ),
                      const SizedBox(width: 10),
                      FilledButton(
                        onPressed: _isSaving ? null : _submit,
                        style: FilledButton.styleFrom(
                          backgroundColor: primary,
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 20, vertical: 10),
                          elevation: 0,
                        ),
                        child: _isSaving
                            ? SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: isDark
                                        ? AppColors.darkBackground
                                        : Colors.white),
                              )
                            : Text(
                                _isEditing ? 'Spremi Izmjene' : 'Kreiraj Kategoriju',
                                style: TextStyle(
                                    fontWeight: FontWeight.w600,
                                    fontSize: 13,
                                    color: isDark
                                        ? AppColors.darkBackground
                                        : Colors.white),
                              ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// ── Helpers ──────────────────────────────────────────────────

class _Label extends StatelessWidget {
  final String text;
  const _Label(this.text);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Text(
      text,
      style: TextStyle(
        fontSize: 12,
        fontWeight: FontWeight.w600,
        color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
      ),
    );
  }
}

class _IconPickerTile extends StatelessWidget {
  final File? iconFile;
  final String? fileName;
  final VoidCallback onTap;

  const _IconPickerTile({
    required this.iconFile,
    required this.fileName,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final picked = iconFile != null;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
          border: Border.all(
            color: picked
                ? primary
                : (isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
            width: picked ? 2 : 1,
          ),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: picked
                    ? primary.withValues(alpha: 0.1)
                    : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
                borderRadius: BorderRadius.circular(8),
              ),
              child: picked
                  ? ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: Image.file(iconFile!, fit: BoxFit.cover),
                    )
                  : Icon(LucideIcons.upload,
                      color: isDark
                          ? AppColors.darkTextDisabled
                          : AppColors.lightTextDisabled,
                      size: 18),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    picked ? (fileName ?? 'Odabrana ikona') : 'Kliknite za odabir ikone',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: picked
                          ? (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary)
                          : (isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    'PNG (maks. 100x100px)',
                    style: TextStyle(
                        fontSize: 11,
                        color: isDark
                            ? AppColors.darkTextDisabled
                            : AppColors.lightTextDisabled),
                  ),
                ],
              ),
            ),
            Icon(LucideIcons.folderOpen,
                size: 16,
                color: picked
                    ? primary
                    : (isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled)),
          ],
        ),
      ),
    );
  }
}
