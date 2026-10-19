// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'providers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Pins dos PCs pareados, usados por todo TLS do app (ver `pinnedHttpClient`).

@ProviderFor(pinRegistry)
final pinRegistryProvider = PinRegistryProvider._();

/// Pins dos PCs pareados, usados por todo TLS do app (ver `pinnedHttpClient`).

final class PinRegistryProvider extends $FunctionalProvider<PinRegistry, PinRegistry, PinRegistry>
    with $Provider<PinRegistry> {
  /// Pins dos PCs pareados, usados por todo TLS do app (ver `pinnedHttpClient`).
  PinRegistryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'pinRegistryProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$pinRegistryHash();

  @$internal
  @override
  $ProviderElement<PinRegistry> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  PinRegistry create(Ref ref) {
    return pinRegistry(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PinRegistry value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<PinRegistry>(value));
  }
}

String _$pinRegistryHash() => r'ebcd51689522a9b7530db00adefd4488c59a30bf';

/// Sobrescrito em `main.dart` com as preferências já carregadas.

@ProviderFor(keyValueStore)
final keyValueStoreProvider = KeyValueStoreProvider._();

/// Sobrescrito em `main.dart` com as preferências já carregadas.

final class KeyValueStoreProvider extends $FunctionalProvider<KeyValueStore, KeyValueStore, KeyValueStore>
    with $Provider<KeyValueStore> {
  /// Sobrescrito em `main.dart` com as preferências já carregadas.
  KeyValueStoreProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'keyValueStoreProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$keyValueStoreHash();

  @$internal
  @override
  $ProviderElement<KeyValueStore> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  KeyValueStore create(Ref ref) {
    return keyValueStore(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(KeyValueStore value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<KeyValueStore>(value));
  }
}

String _$keyValueStoreHash() => r'43c27e439ed5edc740dfeaa0864d2a2daefc9209';

@ProviderFor(secretStore)
final secretStoreProvider = SecretStoreProvider._();

final class SecretStoreProvider extends $FunctionalProvider<SecretStore, SecretStore, SecretStore>
    with $Provider<SecretStore> {
  SecretStoreProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'secretStoreProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$secretStoreHash();

  @$internal
  @override
  $ProviderElement<SecretStore> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  SecretStore create(Ref ref) {
    return secretStore(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(SecretStore value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<SecretStore>(value));
  }
}

String _$secretStoreHash() => r'f6ba1ced2e985c77ee9b83ea18f3bf60168186cd';

/// Relógio do app; os testes sobrescrevem para controlar o tempo.

@ProviderFor(clock)
final clockProvider = ClockProvider._();

/// Relógio do app; os testes sobrescrevem para controlar o tempo.

final class ClockProvider extends $FunctionalProvider<DateTime Function(), DateTime Function(), DateTime Function()>
    with $Provider<DateTime Function()> {
  /// Relógio do app; os testes sobrescrevem para controlar o tempo.
  ClockProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'clockProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$clockHash();

  @$internal
  @override
  $ProviderElement<DateTime Function()> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  DateTime Function() create(Ref ref) {
    return clock(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(DateTime Function() value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<DateTime Function()>(value));
  }
}

String _$clockHash() => r'3f65ad34ac6fcd532de9004042bdf2ed2bd85b13';

/// Emite quando a rede volta (Wi-Fi ou dados), para reconectar na hora em vez de esperar o próximo intervalo.

@ProviderFor(networkAvailable)
final networkAvailableProvider = NetworkAvailableProvider._();

/// Emite quando a rede volta (Wi-Fi ou dados), para reconectar na hora em vez de esperar o próximo intervalo.

final class NetworkAvailableProvider extends $FunctionalProvider<AsyncValue<void>, void, Stream<void>>
    with $FutureModifier<void>, $StreamProvider<void> {
  /// Emite quando a rede volta (Wi-Fi ou dados), para reconectar na hora em vez de esperar o próximo intervalo.
  NetworkAvailableProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'networkAvailableProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$networkAvailableHash();

  @$internal
  @override
  $StreamProviderElement<void> $createElement($ProviderPointer pointer) => $StreamProviderElement(pointer);

  @override
  Stream<void> create(Ref ref) {
    return networkAvailable(ref);
  }
}

String _$networkAvailableHash() => r'9dba33ae015b7282a1c92a9fd95f406571ba2293';
