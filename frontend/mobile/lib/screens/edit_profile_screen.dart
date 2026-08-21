import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/requests/update_user_request.dart';
import '../services/api_exception.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import '../widgets/labeled_field.dart';
import '../widgets/responsive_page.dart';

/// Edits the signed-in user's own first/last name, username and phone —
/// mirrors `UpdateUserRequestValidator` on the backend field-for-field (see
/// [[00-workflow-and-testing]]: the backend validator is the source of
/// truth, this is just its UX mirror).
class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key});

  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends State<EditProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _firstNameCtrl;
  late final TextEditingController _lastNameCtrl;
  late final TextEditingController _usernameCtrl;
  late final TextEditingController _phoneCtrl;
  bool _isLoading = false;
  // Uniqueness can only be checked server-side — surfaced on the field
  // itself rather than a generic toast, per the frontend validation rules.
  String? _usernameError;

  @override
  void initState() {
    super.initState();
    final user = Session.currentUser.value;
    _firstNameCtrl = TextEditingController(text: user?.firstName ?? '');
    _lastNameCtrl = TextEditingController(text: user?.lastName ?? '');
    _usernameCtrl = TextEditingController(text: user?.username ?? '');
    _phoneCtrl = TextEditingController(text: user?.phoneNumber ?? '');
  }

  @override
  void dispose() {
    _firstNameCtrl.dispose();
    _lastNameCtrl.dispose();
    _usernameCtrl.dispose();
    _phoneCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _usernameError = null);
    if (!_formKey.currentState!.validate()) return;

    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Spremi izmjene',
      message: 'Da li ste sigurni da želite sačuvati izmjene svog profila?',
      confirmLabel: 'Spremi',
      destructive: false,
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isLoading = true);

    try {
      await AuthService().updateUser(UpdateUserRequest(
        firstName: _firstNameCtrl.text.trim(),
        lastName: _lastNameCtrl.text.trim(),
        username: _usernameCtrl.text.trim(),
        phoneNumber: _phoneCtrl.text.trim().isEmpty ? null : _phoneCtrl.text.trim(),
      ));

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Podaci su sačuvani.'), backgroundColor: AppColors.success),
      );
      Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.apiError.code == 'user.already_exists') {
        setState(() => _usernameError = e.apiError.displayMessage);
      } else {
        _showError(e.apiError.displayMessage);
      }
    } catch (_) {
      if (mounted) _showError('Čuvanje podataka nije uspjelo. Pokušajte ponovo.');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: AppColors.errorDark),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Lični podaci')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 8),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: LabeledField(
                          label: 'Ime',
                          controller: _firstNameCtrl,
                          validator: (v) => (v == null || v.trim().length < 2) ? 'Obavezno' : null,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: LabeledField(
                          label: 'Prezime',
                          controller: _lastNameCtrl,
                          validator: (v) => (v == null || v.trim().length < 2) ? 'Obavezno' : null,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  LabeledField(
                    label: 'Korisničko ime',
                    controller: _usernameCtrl,
                    prefixIcon: const Icon(Icons.alternate_email_rounded),
                    errorText: _usernameError,
                    validator: (v) => (v == null || v.trim().length < 3) ? 'Minimalno 3 karaktera' : null,
                  ),
                  const SizedBox(height: 16),
                  LabeledField(
                    label: 'Broj telefona (opciono)',
                    controller: _phoneCtrl,
                    keyboardType: TextInputType.phone,
                    prefixIcon: const Icon(Icons.phone_outlined),
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return null;
                      return RegExp(r'^\+?[0-9\s\-()]{6,20}$').hasMatch(v.trim())
                          ? null
                          : 'Broj telefona nije u ispravnom formatu';
                    },
                  ),
                  const SizedBox(height: 24),
                  FilledButton(
                    style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 15)),
                    onPressed: _isLoading ? null : _submit,
                    child: _isLoading
                        ? SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Theme.of(context).colorScheme.onPrimary,
                            ),
                          )
                        : const Text('Sačuvajte promjene'),
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
