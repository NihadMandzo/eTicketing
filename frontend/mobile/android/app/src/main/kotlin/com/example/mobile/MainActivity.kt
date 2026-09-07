package com.example.mobile

import io.flutter.embedding.android.FlutterFragmentActivity

// FlutterFragmentActivity, not FlutterActivity: flutter_stripe presents 3-D Secure and the payment
// sheet as Android Fragments, and they cannot attach to a plain FlutterActivity. Reverting this
// makes every card payment crash the moment the sheet opens.
class MainActivity : FlutterFragmentActivity()
