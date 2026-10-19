import 'dart:async';
import 'dart:io';

import 'package:signalr_netcore/iretry_policy.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../contracts/contracts.dart';
import 'pin_registry.dart';
import 'pinned_signalr.dart';

/// Conexão com o Hub `/hubs/remote` de um PC.
///
/// Negociação e WebSocket passam pelo mesmo `HttpClient` com pinning (risco R4), com o token no header
/// `Authorization` nos dois.
abstract interface class AgentHub {
  Future<void> start();

  Future<void> stop();

  /// Eventos do servidor com esse nome (ex.: `DevicePermissionsChanged`), entregues na hora. Vários ouvintes podem
  /// assinar; o fluxo termina quando a conexão para.
  Stream<List<Object?>> events(String event);

  Future<HubResult<T>> invoke<T>(String method, List<Object> arguments, T Function(Object? data) parse);
}

class SignalRAgentHub implements AgentHub {
  factory SignalRAgentHub({
    required String host,
    required int port,
    required String token,
    required PinRegistry pins,
    required void Function(Object? error) onClosed,
  }) {
    final client = pinnedHttpClient(pins);
    Future<String> accessToken() async => token;
    final connection = HubConnectionBuilder()
        .withUrl(
          'https://$host:$port/hubs/remote',
          options: HttpConnectionOptions(
            httpClient: PinnedSignalRHttpClient(client),
            transport: PinnedWebSocketTransport(client: client, accessToken: accessToken),
            accessTokenFactory: accessToken,
            requestTimeout: 15000,
          ),
        )
        .withAutomaticReconnect(reconnectPolicy: const _NoReconnect())
        .build();
    return SignalRAgentHub._(connection, client, onClosed);
  }

  SignalRAgentHub._(this._connection, this._client, void Function(Object? error) onClosed) {
    _connection.onclose(({error}) => onClosed(error));
  }

  final HubConnection _connection;
  final HttpClient _client;
  final Map<String, StreamController<List<Object?>>> _events = {};

  @override
  Future<void> start() async {
    try {
      await _connection.start();
    } on Object {
      _client.close(force: true);
      rethrow;
    }
  }

  @override
  Future<void> stop() async {
    try {
      await _connection.stop();
    } finally {
      _client.close(force: true);
      for (final controller in _events.values) {
        unawaited(controller.close());
      }
    }
  }

  @override
  Stream<List<Object?>> events(String event) => _events.putIfAbsent(event, () {
    final controller = StreamController<List<Object?>>.broadcast(sync: true);
    _connection.on(event, (arguments) => controller.add(arguments ?? const []));
    return controller;
  }).stream;

  @override
  Future<HubResult<T>> invoke<T>(String method, List<Object> arguments, T Function(Object? data) parse) async =>
      HubResult.fromJson(await _connection.invoke(method, args: arguments), parse);
}

/// Sem reconexão do `signalr_netcore`: ele usa uma política padrão mesmo sem `withAutomaticReconnect` e só avisaria a
/// queda depois de ~42 s. Quem reconecta é o `AgentConnectionController` (troca de endereço, backoff, rede).
class _NoReconnect implements IRetryPolicy {
  const _NoReconnect();

  @override
  int? nextRetryDelayInMilliseconds(RetryContext retryContext) => null;
}
