@Tags(['e2e'])
library;

import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/core/network/agent_api.dart';
import 'package:my_remote_controll/core/network/agent_hub.dart';
import 'package:my_remote_controll/core/network/pin_registry.dart';
import 'package:my_remote_controll/core/network/pinned_signalr.dart';
import 'package:my_remote_controll/core/network/spki.dart';
import 'package:signalr_netcore/signalr_client.dart' show TransferFormat;

/// Testes contra um agente real rodando no PC (risco R4: pinning no REST, na negociação e no WebSocket do Hub).
///
/// Rode o agente e depois:
///   MRC_E2E_HOST=127.0.0.1 MRC_E2E_PORT=47800 flutter test --tags e2e
void main() {
  final host = Platform.environment['MRC_E2E_HOST'];
  final port = int.tryParse(Platform.environment['MRC_E2E_PORT'] ?? '');
  final skip = host == null || port == null ? 'defina MRC_E2E_HOST e MRC_E2E_PORT com um agente rodando' : null;

  /// Lê o pin do certificado servido (só para o teste; o app recebe o pin pelo QR).
  Future<String> servedPin() async {
    X509Certificate? served;
    final socket = await SecureSocket.connect(
      host!,
      port!,
      onBadCertificate: (certificate) {
        served = certificate;
        return true;
      },
    );
    served ??= socket.peerCertificate;
    await socket.close();
    return spkiPin(served!.der);
  }

  test('REST com o pin certo funciona; com pin diferente, a conexão é bloqueada como possível ataque', () async {
    final pin = await servedPin();

    final trusted = PinRegistry()..register([host!], port!, pin);
    final info = await AgentApi(host: host, port: port, pins: trusted).getInfo();
    expect(info.apiVersion, '1');

    final attacker = PinRegistry()..register([host], port, 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA');
    await expectLater(
      AgentApi(host: host, port: port, pins: attacker).getInfo(),
      throwsA(isA<AgentException>().having((e) => e.kind, 'kind', AgentFailureKind.certificateMismatch)),
    );
  }, skip: skip);

  test('o Hub (negociação e WebSocket) só conecta com o pin certo', () async {
    final pin = await servedPin();

    // Pin errado: a conexão cai no TLS e a divergência fica registrada.
    final attacker = PinRegistry()..register([host!], port!, 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA');
    final blocked = SignalRAgentHub(host: host, port: port, token: 'token-invalido', pins: attacker, onClosed: (_) {});
    await expectLater(blocked.start(), throwsA(isA<HandshakeException>()));
    expect(attacker.hadMismatch(host, port), isTrue);

    // Pin certo e token inválido: o TLS passa e quem recusa é a autenticação do agente.
    final trusted = PinRegistry()..register([host], port, pin);
    final rejectedByAuth = SignalRAgentHub(
      host: host,
      port: port,
      token: 'token-invalido',
      pins: trusted,
      onClosed: (_) {},
    );
    await expectLater(rejectedByAuth.start(), throwsA(predicate((e) => '$e'.contains('401'))));
    expect(trusted.hadMismatch(host, port), isFalse);

    // O upgrade do WebSocket confere o pin por conta própria (não só a negociação).
    Future<void> upgrade(PinRegistry pins) => PinnedWebSocketTransport(
      client: pinnedHttpClient(pins),
      accessToken: () async => 'token-invalido',
    ).connect('https://$host:$port/hubs/remote', TransferFormat.Text);
    final attackerOnSocket = PinRegistry()..register([host], port, 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA');
    await expectLater(upgrade(attackerOnSocket), throwsA(isA<HandshakeException>()));
    expect(attackerOnSocket.hadMismatch(host, port), isTrue);
    await expectLater(
      upgrade(trusted),
      throwsA(isA<WebSocketException>().having((e) => '$e', 'texto', contains('401'))),
    );
  }, skip: skip);
}
