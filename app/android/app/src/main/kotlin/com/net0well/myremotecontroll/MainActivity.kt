package com.net0well.myremotecontroll

import android.os.PowerManager
import io.flutter.embedding.android.FlutterFragmentActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

// FlutterFragmentActivity é exigida pelo local_auth (biometria).
class MainActivity : FlutterFragmentActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Economia de bateria: o painel reduz o ritmo para 3 s (spec 007).
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.net0well.myremotecontroll/power_mode")
            .setMethodCallHandler { call, result ->
                if (call.method == "isPowerSaveMode") {
                    result.success(getSystemService(PowerManager::class.java)?.isPowerSaveMode ?: false)
                } else {
                    result.notImplemented()
                }
            }
    }
}
