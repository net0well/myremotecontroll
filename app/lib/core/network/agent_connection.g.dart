// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'agent_connection.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Intervalos entre tentativas de reconexão; os testes sobrescrevem com valores curtos.

@ProviderFor(reconnectDelays)
final reconnectDelaysProvider = ReconnectDelaysProvider._();

/// Intervalos entre tentativas de reconexão; os testes sobrescrevem com valores curtos.

final class ReconnectDelaysProvider extends $FunctionalProvider<List<Duration>, List<Duration>, List<Duration>>
    with $Provider<List<Duration>> {
  /// Intervalos entre tentativas de reconexão; os testes sobrescrevem com valores curtos.
  ReconnectDelaysProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'reconnectDelaysProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$reconnectDelaysHash();

  @$internal
  @override
  $ProviderElement<List<Duration>> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  List<Duration> create(Ref ref) {
    return reconnectDelays(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(List<Duration> value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<List<Duration>>(value));
  }
}

String _$reconnectDelaysHash() => r'40f4d446310b4a203d9acc50bfb5bc4d69c120d6';

@ProviderFor(agentTransport)
final agentTransportProvider = AgentTransportProvider._();

final class AgentTransportProvider extends $FunctionalProvider<AgentTransport, AgentTransport, AgentTransport>
    with $Provider<AgentTransport> {
  AgentTransportProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'agentTransportProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$agentTransportHash();

  @$internal
  @override
  $ProviderElement<AgentTransport> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  AgentTransport create(Ref ref) {
    return agentTransport(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(AgentTransport value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<AgentTransport>(value));
  }
}

String _$agentTransportHash() => r'c1ee7db88c4ef6173f4b9df9d126ae662c0ca979';

/// Mantém a conexão com o PC ativo: tenta todos os endereços em paralelo, reconecta com espera exponencial e na volta
/// da rede, e troca de PC na hora (spec 004, AC-1 e AC-2).

@ProviderFor(AgentConnectionController)
final agentConnectionControllerProvider = AgentConnectionControllerProvider._();

/// Mantém a conexão com o PC ativo: tenta todos os endereços em paralelo, reconecta com espera exponencial e na volta
/// da rede, e troca de PC na hora (spec 004, AC-1 e AC-2).
final class AgentConnectionControllerProvider extends $NotifierProvider<AgentConnectionController, AgentConnection> {
  /// Mantém a conexão com o PC ativo: tenta todos os endereços em paralelo, reconecta com espera exponencial e na volta
  /// da rede, e troca de PC na hora (spec 004, AC-1 e AC-2).
  AgentConnectionControllerProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'agentConnectionControllerProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$agentConnectionControllerHash();

  @$internal
  @override
  AgentConnectionController create() => AgentConnectionController();

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(AgentConnection value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<AgentConnection>(value));
  }
}

String _$agentConnectionControllerHash() => r'e4eed330ebf86bdb0a17da5b8923267dc640af5f';

/// Mantém a conexão com o PC ativo: tenta todos os endereços em paralelo, reconecta com espera exponencial e na volta
/// da rede, e troca de PC na hora (spec 004, AC-1 e AC-2).

abstract class _$AgentConnectionController extends $Notifier<AgentConnection> {
  AgentConnection build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<AgentConnection, AgentConnection>;
    final element =
        ref.element
            as $ClassProviderElement<AnyNotifier<AgentConnection, AgentConnection>, AgentConnection, Object?, Object?>;
    return element.handleCreate(ref, build);
  }
}
