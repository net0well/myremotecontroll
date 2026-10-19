import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/services.dart';

/// Se o painel deve economizar (spec 007): dados móveis ou economia de bateria → 3 s e dados reduzidos.
abstract interface class DataSaver {
  Future<bool> isConstrained();

  /// Emite quando a rede muda (Wi-Fi ↔ dados), para o painel reavaliar o ritmo.
  Stream<void> get changes;
}

/// Dados móveis pelo `connectivity_plus`; economia de bateria por um canal próprio (Android `PowerManager`, iOS
/// `isLowPowerModeEnabled`), porque o `battery_plus` não resolve junto com o `connectivity_plus` atual.
class PlatformDataSaver implements DataSaver {
  PlatformDataSaver({Connectivity? connectivity}) : _connectivity = connectivity ?? Connectivity();

  static const _channel = MethodChannel('com.net0well.myremotecontroll/power_mode');

  final Connectivity _connectivity;

  @override
  Stream<void> get changes => _connectivity.onConnectivityChanged.map((_) {});

  @override
  Future<bool> isConstrained() async {
    final results = await _connectivity.checkConnectivity();
    final mobileOnly =
        results.contains(ConnectivityResult.mobile) &&
        !results.contains(ConnectivityResult.wifi) &&
        !results.contains(ConnectivityResult.ethernet);
    return mobileOnly || await _powerSaveMode();
  }

  Future<bool> _powerSaveMode() async {
    try {
      return await _channel.invokeMethod<bool>('isPowerSaveMode') ?? false;
    } on PlatformException {
      return false;
    } on MissingPluginException {
      return false;
    }
  }
}
