// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pairing_screen.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Os testes de widget desligam a câmera (o scanner usa canais de plataforma).

@ProviderFor(qrScannerEnabled)
final qrScannerEnabledProvider = QrScannerEnabledProvider._();

/// Os testes de widget desligam a câmera (o scanner usa canais de plataforma).

final class QrScannerEnabledProvider extends $FunctionalProvider<bool, bool, bool> with $Provider<bool> {
  /// Os testes de widget desligam a câmera (o scanner usa canais de plataforma).
  QrScannerEnabledProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'qrScannerEnabledProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$qrScannerEnabledHash();

  @$internal
  @override
  $ProviderElement<bool> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  bool create(Ref ref) {
    return qrScannerEnabled(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(bool value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<bool>(value));
  }
}

String _$qrScannerEnabledHash() => r'8cebc169dc1b9ceb6d0fbd7c119ab1cafedcc1c6';
