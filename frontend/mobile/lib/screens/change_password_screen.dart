import 'package:flutter/material.dart';

import '../models/requests/change_password_request.dart';
import '../services/api_exception.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';
import '../widgets/labeled_field.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import '../utils/validators.dart';

/// Mirrors `ChangePasswordRequestValidator` on the backend field-for-field.
/// On success the backend revokes every active refresh token for this
/// account (see AuthService.ChangePasswordAsync), so this screen signs the
/// user out and returns to the (login-gated) login screen rather than
/// pretending the current session is still good.
class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _currentPasswordCtrl = TextEditingController();
  final _newPasswordCtrl = TextEditingController();
  final _confirmPasswordCtrl = TextEditingController();
  bool _isLoading = false;
  // Only the backend can confirm the current password is correct — surfaced
  // on the field itself rather than a generic toast.
  String? _currentPasswordError;

  @override
  void dispose() {
    _currentPasswordCtrl.dispose();
    _newPasswordCtrl.dispose();
    _confirmPasswordCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _currentPasswordError = null);
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isLoading = true);

    // Captured before the pop below — MaterialApp's ScaffoldMessenger sits
    // above the Navigator, so this reference stays valid after navigating.
    final messenger = ScaffoldMessenger.of(context);

    try {
      await AuthService().changePassword(ChangePasswordRequest(
        currentPassword: _currentPasswordCtrl.text,
        newPassword: _newPasswordCtrl.text,
        confirmPassword: _confirmPasswordCtrl.text,
      ));

      if (!mounted) return;
      await AuthService().logout();
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const LoginScreen()),
        (route) => false,
      );
      messenger.showSnackBar(
        const SnackBar(
          content: Text('Lozinka je promijenjena. Prijavite se ponovo.'),
          backgroundColor: AppColors.success,
        ),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.apiError.code == 'auth.wrong_current_password') {
        setState(() => _currentPasswordError = e.apiError.displayMessage);
      } else {
        _showError(e.apiError.displayMessage);
      }
    } catch (_) {
      if (mounted) _showError('Promjena lozinke nije uspjela. Pokušajte ponovo.');
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
      appBar: AppBar(title: const Text('Promijeni lozinku')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: ResponsivePage(
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 8),
                  LabeledPasswordField(
                    label: 'Trenutna lozinka',
                    controller: _currentPasswordCtrl,
                    errorText: _currentPasswordError,
                    validator: (v) => (v == null || v.isEmpty) ? 'Unesite trenutnu lozinku' : null,
                  ),
                  const SizedBox(height: 16),
                  LabeledPasswordField(
                    label: 'Nova lozinka',
                    controller: _newPasswordCtrl,
                    validator: Validators.password,
                  ),
                  const SizedBox(height: 16),
                  LabeledPasswordField(
                    label: 'Potvrdite novu lozinku',
                    controller: _confirmPasswordCtrl,
                    validator: (v) => (v != _newPasswordCtrl.text) ? 'Lozinke se ne podudaraju' : null,
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
                        : const Text('Promijenite lozinku'),
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
