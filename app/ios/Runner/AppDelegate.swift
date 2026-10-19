import Flutter
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)

    // Modo de pouca energia: o painel reduz o ritmo para 3 s (spec 007).
    if let registrar = engineBridge.pluginRegistry.registrar(forPlugin: "PowerModeChannel") {
      let channel = FlutterMethodChannel(
        name: "com.net0well.myremotecontroll/power_mode", binaryMessenger: registrar.messenger())
      channel.setMethodCallHandler { call, result in
        if call.method == "isPowerSaveMode" {
          result(ProcessInfo.processInfo.isLowPowerModeEnabled)
        } else {
          result(FlutterMethodNotImplemented)
        }
      }
    }
  }
}
