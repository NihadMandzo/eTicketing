import 'package:flutter/foundation.dart';

import '../models/responses/user_response.dart';

/// App-wide auth state. Guest browsing is always allowed — nothing in this
/// app gates *navigation* on [currentUser]; screens/actions that specifically
/// require a signed-in user check it themselves at the call site.
class Session {
  Session._();

  static final ValueNotifier<UserResponse?> currentUser = ValueNotifier(null);

  static bool get isAuthenticated => currentUser.value != null;
}
