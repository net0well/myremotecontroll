// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'power_repository.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Repositório da sessão online; nulo sem conexão.

@ProviderFor(powerRepository)
final powerRepositoryProvider = PowerRepositoryProvider._();

/// Repositório da sessão online; nulo sem conexão.

final class PowerRepositoryProvider extends $FunctionalProvider<PowerRepository?, PowerRepository?, PowerRepository?>
    with $Provider<PowerRepository?> {
  /// Repositório da sessão online; nulo sem conexão.
  PowerRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'powerRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$powerRepositoryHash();

  @$internal
  @override
  $ProviderElement<PowerRepository?> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  PowerRepository? create(Ref ref) {
    return powerRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PowerRepository? value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<PowerRepository?>(value));
  }
}

String _$powerRepositoryHash() => r'562028b08d684f8a43cb9ee6f29326c2831b280d';

/// Contagens que terminaram sem executar, para avisar na tela (cancelada no PC, falha do Windows…).

@ProviderFor(powerCancellations)
final powerCancellationsProvider = PowerCancellationsProvider._();

/// Contagens que terminaram sem executar, para avisar na tela (cancelada no PC, falha do Windows…).

final class PowerCancellationsProvider
    extends $FunctionalProvider<AsyncValue<PowerCancelled>, PowerCancelled, Stream<PowerCancelled>>
    with $FutureModifier<PowerCancelled>, $StreamProvider<PowerCancelled> {
  /// Contagens que terminaram sem executar, para avisar na tela (cancelada no PC, falha do Windows…).
  PowerCancellationsProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'powerCancellationsProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$powerCancellationsHash();

  @$internal
  @override
  $StreamProviderElement<PowerCancelled> $createElement($ProviderPointer pointer) => $StreamProviderElement(pointer);

  @override
  Stream<PowerCancelled> create(Ref ref) {
    return powerCancellations(ref);
  }
}

String _$powerCancellationsHash() => r'c9b6c84c7ae2e896eed60e3ebbba1db48153e1c4';
