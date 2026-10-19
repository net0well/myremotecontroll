import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/network/agent_hub.dart';
import 'package:my_remote_controll/core/network/pin_registry.dart';
import 'package:my_remote_controll/core/network/pinned_signalr.dart';
import 'package:my_remote_controll/core/network/spki.dart';
import 'package:signalr_netcore/signalr_client.dart' show TransferFormat;

const _recordSeparator = '\u001e';
const _wrongPin = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA';

/// Servidor TLS local com o mínimo do protocolo do SignalR (negociação, handshake JSON e invocações), para testar o
/// Hub do app sem o agente. O certificado de `test/fixtures/test_server.*` é só de teste.
class _FakeSignalRServer {
  _FakeSignalRServer._(this._server);

  static Future<_FakeSignalRServer> start() async {
    final context = SecurityContext()
      ..useCertificateChain('test/fixtures/test_server.pem')
      ..usePrivateKey('test/fixtures/test_server.key');
    final server = _FakeSignalRServer._(await HttpServer.bindSecure(InternetAddress.loopbackIPv4, 0, context));
    server._server.listen(server._handle);
    return server;
  }

  final HttpServer _server;
  final List<String> requests = [];
  final List<String?> upgradeAuthorizations = [];
  WebSocket? socket;

  int get port => _server.port;

  static String get pin {
    final pem = File('test/fixtures/test_server.pem').readAsStringSync();
    final body = pem
        .replaceAll('-----BEGIN CERTIFICATE-----', '')
        .replaceAll('-----END CERTIFICATE-----', '')
        .replaceAll(RegExp(r'\s'), '');
    return spkiPin(base64.decode(body));
  }

  Future<void> close() async {
    await socket?.close();
    await _server.close(force: true);
  }

  void send(Map<String, Object?> message) => socket!.add('${jsonEncode(message)}$_recordSeparator');

  Future<void> _handle(HttpRequest request) async {
    requests.add('${request.method} ${request.uri.path}');
    final authorized = request.headers.value(HttpHeaders.authorizationHeader) == 'Bearer token-valido';

    if (request.uri.path == '/hubs/remote/negotiate') {
      request.response.statusCode = authorized ? HttpStatus.ok : HttpStatus.unauthorized;
      if (authorized) {
        request.response.write(
          jsonEncode({
            'negotiateVersion': 1,
            'connectionId': 'c1',
            'connectionToken': 't1',
            'availableTransports': [
              {
                'transport': 'WebSockets',
                'transferFormats': ['Text', 'Binary'],
              },
            ],
          }),
        );
      }
      await request.response.close();
      return;
    }

    upgradeAuthorizations.add(request.headers.value(HttpHeaders.authorizationHeader));
    if (!authorized || request.uri.queryParameters['id'] != 't1') {
      request.response.statusCode = HttpStatus.unauthorized;
      await request.response.close();
      return;
    }

    _serve(socket = await WebSocketTransformer.upgrade(request));
  }

  void _serve(WebSocket socket) {
    socket.listen((data) {
      for (final frame in (data as String).split(_recordSeparator).where((f) => f.isNotEmpty)) {
        final message = jsonDecode(frame) as Map<String, Object?>;
        if (message.containsKey('protocol')) {
          socket.add('{}$_recordSeparator');
        } else if (message['type'] == 1) {
          send({
            'type': 3,
            'invocationId': message['invocationId'],
            'result': {'ok': true, 'data': message['arguments']},
          });
        }
      }
    });
  }
}

void main() {
  late _FakeSignalRServer server;

  setUp(() async => server = await _FakeSignalRServer.start());
  tearDown(() => server.close());

  SignalRAgentHub hub(PinRegistry pins, {String token = 'token-valido', void Function(Object?)? onClosed}) =>
      SignalRAgentHub(host: '127.0.0.1', port: server.port, token: token, pins: pins, onClosed: onClosed ?? (_) {});

  PinRegistry pinned(String pin) => PinRegistry()..register(['127.0.0.1'], server.port, pin);

  test('com o pin certo, negocia, abre o WebSocket com o token no header, invoca e recebe eventos', () async {
    final connection = hub(pinned(_FakeSignalRServer.pin));
    final events = <List<Object?>>[];
    connection.events('DevicePermissionsChanged').listen(events.add);

    await connection.start();
    final result = await connection.invoke('Echo', ['oi'], (data) => data);
    server.send({
      'type': 1,
      'target': 'DevicePermissionsChanged',
      'arguments': ['power.shutdown'],
    });
    await Future<void>.delayed(const Duration(milliseconds: 100));
    await connection.stop();

    expect(server.requests, ['POST /hubs/remote/negotiate', 'GET /hubs/remote']);
    expect(server.upgradeAuthorizations, ['Bearer token-valido']);
    expect(result.data, ['oi']);
    expect(events, [
      ['power.shutdown'],
    ]);
  });

  test('com o pin errado, o TLS é recusado antes de qualquer requisição e a divergência fica registrada', () async {
    final pins = pinned(_wrongPin);

    await expectLater(hub(pins).start(), throwsA(isA<HandshakeException>()));

    expect(server.requests, isEmpty);
    expect(pins.hadMismatch('127.0.0.1', server.port), isTrue);
  });

  test('o WebSocket confere o pin por conta própria, sem depender da negociação', () async {
    final pins = pinned(_wrongPin);
    final transport = PinnedWebSocketTransport(client: pinnedHttpClient(pins), accessToken: () async => 'token-valido');

    await expectLater(
      transport.connect('https://127.0.0.1:${server.port}/hubs/remote?id=t1', TransferFormat.Text),
      throwsA(isA<HandshakeException>()),
    );

    expect(server.requests, isEmpty);
    expect(pins.hadMismatch('127.0.0.1', server.port), isTrue);
  });

  test('host sem pin registrado é recusado', () async {
    await expectLater(hub(PinRegistry()).start(), throwsA(isA<HandshakeException>()));

    expect(server.requests, isEmpty);
  });

  test('queda do WebSocket pelo servidor avisa onClosed na hora, sem reconexão própria do signalr_netcore', () async {
    final closed = Completer<Object?>();
    final connection = hub(pinned(_FakeSignalRServer.pin), onClosed: closed.complete);
    await connection.start();

    await server.socket!.close(WebSocketStatus.internalServerError);

    expect(await closed.future.timeout(const Duration(seconds: 2)), isNotNull);
    await Future<void>.delayed(const Duration(milliseconds: 200));
    expect(server.requests, ['POST /hubs/remote/negotiate', 'GET /hubs/remote']);
  });
}
