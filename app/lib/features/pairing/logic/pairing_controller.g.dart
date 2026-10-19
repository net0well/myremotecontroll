// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pairing_controller.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(pairingService)
final pairingServiceProvider = PairingServiceProvider._();

final class PairingServiceProvider extends $FunctionalProvider<PairingService, PairingService, PairingService>
    with $Provider<PairingService> {
  PairingServiceProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'pairingServiceProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$pairingServiceHash();

  @$internal
  @override
  $ProviderElement<PairingService> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  PairingService create(Ref ref) {
    return pairingService(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PairingService value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<PairingService>(value));
  }
}

String _$pairingServiceHash() => r'3834161d83a7759300eda3f219ec6dce5247f7e2';

@ProviderFor(PairingController)
final pairingControllerProvider = PairingControllerProvider._();

final class PairingControllerProvider extends $NotifierProvider<PairingController, PairingState> {
  PairingControllerProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'pairingControllerProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$pairingControllerHash();

  @$internal
  @override
  PairingController create() => PairingController();

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PairingState value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<PairingState>(value));
  }
}

String _$pairingControllerHash() => r'c585945af67277ab92edae35baf546dbac1b188c';

abstract class _$PairingController extends $Notifier<PairingState> {
  PairingState build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<PairingState, PairingState>;
    final element =
        ref.element as $ClassProviderElement<AnyNotifier<PairingState, PairingState>, PairingState, Object?, Object?>;
    return element.handleCreate(ref, build);
  }
}
