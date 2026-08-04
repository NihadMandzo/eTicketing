import 'package:flutter/material.dart';

class SnackbarService {
  SnackbarService._();

  static final GlobalKey<NavigatorState> navigatorKey =
      GlobalKey<NavigatorState>();

  static void showSuccess(String message) {
    _show(message: message, isError: false);
  }

  static void showError(String message) {
    _show(message: message, isError: true);
  }

  static void Function()? _removeCurrent;

  static void _show({required String message, required bool isError}) {
    final overlayState = navigatorKey.currentState?.overlay;
    if (overlayState == null) return;

    // Remove any existing snackbar safely
    _removeCurrent?.call();
    _removeCurrent = null;

    late OverlayEntry entry;
    bool isRemoved = false;

    void safeRemove() {
      if (!isRemoved) {
        isRemoved = true;
        entry.remove();
      }
    }

    entry = OverlayEntry(
      builder: (_) => _TopRightSnackbar(
        message: message,
        isError: isError,
        onDismiss: () {
          safeRemove();
          if (_removeCurrent == safeRemove) {
            _removeCurrent = null;
          }
        },
      ),
    );

    _removeCurrent = safeRemove;
    overlayState.insert(entry);
  }
}

class _TopRightSnackbar extends StatefulWidget {
  final String message;
  final bool isError;
  final VoidCallback onDismiss;

  const _TopRightSnackbar({
    required this.message,
    required this.isError,
    required this.onDismiss,
  });

  @override
  State<_TopRightSnackbar> createState() => _TopRightSnackbarState();
}

class _TopRightSnackbarState extends State<_TopRightSnackbar>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _opacity;
  late final Animation<Offset> _slide;

  static const Duration _animDuration = Duration(milliseconds: 300);
  static const Duration _displayDuration = Duration(seconds: 4);

  @override
  void initState() {
    super.initState();

    _controller = AnimationController(vsync: this, duration: _animDuration);

    _opacity = CurvedAnimation(parent: _controller, curve: Curves.easeOut);

    _slide = Tween<Offset>(
      begin: const Offset(1, 0),
      end: Offset.zero,
    ).animate(CurvedAnimation(parent: _controller, curve: Curves.easeOut));

    _controller.forward();

    Future.delayed(_displayDuration, _dismiss);
  }

  Future<void> _dismiss() async {
    if (!mounted) return;
    await _controller.reverse();
    if (!mounted) return;
    widget.onDismiss();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final color = widget.isError
        ? const Color(0xFFC62828)
        : const Color(0xFF2E7D32);

    final icon = widget.isError
        ? Icons.error_outline_rounded
        : Icons.check_circle_outline_rounded;

    return Positioned(
      top: 24,
      right: 24,
      child: FadeTransition(
        opacity: _opacity,
        child: SlideTransition(
          position: _slide,
          child: Material(
            color: Colors.transparent,
            child: GestureDetector(
              onTap: _dismiss,
              child: Container(
                constraints: const BoxConstraints(maxWidth: 380, minWidth: 260),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                decoration: BoxDecoration(
                  color: color,
                  borderRadius: BorderRadius.circular(10),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.25),
                      blurRadius: 12,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Stack(
                  children: [
                    Padding(
                      padding: const EdgeInsets.only(right: 24.0),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(icon, color: Colors.white, size: 20),
                          const SizedBox(width: 10),
                          Flexible(
                            child: Text(
                              widget.message,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 14,
                                fontWeight: FontWeight.w500,
                                height: 1.4,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    Positioned(
                      top: 2,
                      right: 2,
                      child: GestureDetector(
                        onTap: _dismiss,
                        child: const Icon(Icons.close, color: Colors.white70, size: 16),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
