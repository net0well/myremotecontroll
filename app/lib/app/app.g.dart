// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Cor-base do Material You (Android 12+). Lê só a paleta crua: os tipos de tema do pacote dynamic_color ainda são os
/// do Material do SDK, incompatíveis com o material_ui (risco R10).

@ProviderFor(dynamicSeedColor)
final dynamicSeedColorProvider = DynamicSeedColorProvider._();

/// Cor-base do Material You (Android 12+). Lê só a paleta crua: os tipos de tema do pacote dynamic_color ainda são os
/// do Material do SDK, incompatíveis com o material_ui (risco R10).

final class DynamicSeedColorProvider extends $FunctionalProvider<AsyncValue<Color?>, Color?, FutureOr<Color?>>
    with $FutureModifier<Color?>, $FutureProvider<Color?> {
  /// Cor-base do Material You (Android 12+). Lê só a paleta crua: os tipos de tema do pacote dynamic_color ainda são os
  /// do Material do SDK, incompatíveis com o material_ui (risco R10).
  DynamicSeedColorProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'dynamicSeedColorProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$dynamicSeedColorHash();

  @$internal
  @override
  $FutureProviderElement<Color?> $createElement($ProviderPointer pointer) => $FutureProviderElement(pointer);

  @override
  FutureOr<Color?> create(Ref ref) {
    return dynamicSeedColor(ref);
  }
}

String _$dynamicSeedColorHash() => r'e301661f72059c84c182a9ddd40a6e71ddc5fc0a';
