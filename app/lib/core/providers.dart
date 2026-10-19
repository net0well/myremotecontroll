import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import 'network/pin_registry.dart';
import 'storage/stores.dart';

part 'providers.g.dart';

/// Pins dos PCs pareados, usados por todo TLS do app (ver `pinnedHttpClient`).
@Riverpod(keepAlive: true)
PinRegistry pinRegistry(Ref ref) => PinRegistry();

/// Sobrescrito em `main.dart` com as preferências já carregadas.
@Riverpod(keepAlive: true)
KeyValueStore keyValueStore(Ref ref) =>
    throw UnimplementedError('keyValueStore precisa ser sobrescrito no ProviderScope.');

@Riverpod(keepAlive: true)
SecretStore secretStore(Ref ref) => const SecureSecretStore();

/// Relógio do app; os testes sobrescrevem para controlar o tempo.
@Riverpod(keepAlive: true)
DateTime Function() clock(Ref ref) => DateTime.now;

/// Emite quando a rede volta (Wi-Fi ou dados), para reconectar na hora em vez de esperar o próximo intervalo.
@Riverpod(keepAlive: true)
Stream<void> networkAvailable(Ref ref) => Connectivity().onConnectivityChanged
    .where((results) => results.any((r) => r != ConnectivityResult.none))
    .map((_) {});
