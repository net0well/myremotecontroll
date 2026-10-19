import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/contracts/contracts.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/core/network/agent_connection.dart';
import 'package:my_remote_controll/core/storage/paired_pcs.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../helpers/fakes.dart';

/// Espera o estado da conexão satisfazer [test], com limite de tempo.
Future<AgentConnection> waitFor(
  ProviderContainer container,
  bool Function(AgentConnection) test, {
  Duration timeout = const Duration(seconds: 3),
}) {
  final current = container.read(agentConnectionControllerProvider);
  if (test(current)) {
    return Future.value(current);
  }

  final completer = Completer<AgentConnection>();
  final subscription = container.listen(agentConnectionControllerProvider, (_, next) {
    if (test(next) && !completer.isCompleted) {
      completer.complete(next);
    }
  });
  return completer.future.timeout(timeout).whenComplete(subscription.close);
}

void main() {
  late TestEnvironment env;

  setUp(() => env = TestEnvironment());

  test('ac1_trocar de PC reconecta ao novo em até 3 s', () async {
    await env.seedPcs([
      testPc('a', hosts: ['10.0.0.1']),
      testPc('b', hosts: ['10.0.0.2']),
    ], activeId: 'a');
    env.transport
      ..reachable('10.0.0.1')
      ..reachable('10.0.0.2');
    final container = env.container();
    await waitFor(container, (s) => s is OnlineConnection && s.session.pc.id == 'a');

    final watch = Stopwatch()..start();
    await container.read(pairedPcsProvider.notifier).setActive('b');
    final online = await waitFor(container, (s) => s is OnlineConnection && s.session.pc.id == 'b');

    expect(watch.elapsed, lessThan(const Duration(seconds: 3)));
    expect((online as OnlineConnection).session.host, '10.0.0.2');
    expect(env.transport.hubs.first.stopped, isTrue, reason: 'a sessão do PC anterior precisa ser encerrada');
  });

  test('ac2_quando a rede volta, reconecta sozinho e reassina os eventos', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.failing('10.0.0.1', AgentFailureKind.unreachable);
    final container = env.container();
    await waitFor(container, (s) => s is ReconnectingConnection);

    env.transport.reachable('10.0.0.1');
    env.network.add(null);
    await waitFor(container, (s) => s is OnlineConnection);

    expect(env.transport.lastHub.subscribedEvents, containsAll(['DevicePermissionsChanged', 'DeviceRevoked']));
  });

  test('queda do Hub leva a reconexão e volta a ficar online', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1');
    final container = env.container();
    await waitFor(container, (s) => s is OnlineConnection);

    env.transport.lastHub.drop();
    final reconnecting = await waitFor(container, (s) => s is ReconnectingConnection);
    expect((reconnecting as ReconnectingConnection).agentStopped, isFalse);
    expect(env.transport.hubs.first.stopped, isTrue, reason: 'a sessão que caiu precisa ser liberada');
    container.read(agentConnectionControllerProvider.notifier).retryNow();

    await waitFor(container, (s) => s is OnlineConnection);
    expect(env.transport.hubs, hasLength(2));
  });

  test('com AgentShuttingDown, a queda aparece como agente encerrado no PC e o app segue tentando', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1');
    final container = env.container();
    await waitFor(container, (s) => s is OnlineConnection);

    env.transport.lastHub
      ..emit('AgentShuttingDown', ['O agente está sendo encerrado no PC.'])
      ..drop();
    final reconnecting = await waitFor(container, (s) => s is ReconnectingConnection);
    expect((reconnecting as ReconnectingConnection).agentStopped, isTrue);

    container.read(agentConnectionControllerProvider.notifier).retryNow();
    await waitFor(container, (s) => s is OnlineConnection);
    env.transport.lastHub.drop();
    final again = await waitFor(container, (s) => s is ReconnectingConnection);
    expect(
      (again as ReconnectingConnection).agentStopped,
      isFalse,
      reason: 'o aviso vale só para a sessão que o recebeu',
    );
  });

  test('ac5_token revogado leva ao estado de aparelho revogado, sem novas tentativas', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.failing('10.0.0.1', AgentFailureKind.unauthorized, code: ErrorCodes.authRevoked);
    final container = env.container();

    await waitFor(container, (s) => s is RevokedConnection);
    final probes = env.transport.probes;
    env.network.add(null);
    await Future<void>.delayed(const Duration(milliseconds: 50));

    expect(env.transport.probes, probes);
  });

  test('certificado com pin diferente bloqueia a conexão (spec 002, AC-3)', () async {
    await env.seedPcs([
      testPc('a', hosts: ['10.0.0.1', '10.0.0.2']),
    ], activeId: 'a');
    env.transport
      ..failing('10.0.0.1', AgentFailureKind.unreachable)
      ..failing('10.0.0.2', AgentFailureKind.certificateMismatch);
    final container = env.container();

    expect(await waitFor(container, (s) => s is! ConnectingConnection), isA<CertificateMismatchConnection>());
  });

  test('tenta todos os endereços em paralelo e lembra o que respondeu', () async {
    await env.seedPcs([
      testPc('a', hosts: ['10.0.0.1', '100.64.1.2']),
    ], activeId: 'a');
    env.transport
      ..failing('10.0.0.1', AgentFailureKind.unreachable)
      ..reachable('100.64.1.2');
    final container = env.container();

    final online = await waitFor(container, (s) => s is OnlineConnection) as OnlineConnection;
    await Future<void>.delayed(Duration.zero);

    expect(online.session.host, '100.64.1.2');
    expect(container.read(pairedPcsProvider).active!.lastHost, '100.64.1.2');
  });

  test('aparelho revogado pelo Hub sai do estado online na hora', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1');
    final container = env.container();
    await waitFor(container, (s) => s is OnlineConnection);

    env.transport.lastHub.emit('DeviceRevoked', ['device']);

    expect(container.read(agentConnectionControllerProvider), isA<RevokedConnection>());
  });

  test('mudança de permissões chega pelo Hub e atualiza o estado', () async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1', testInfo(permissions: {PermissionKeys.statusView}, role: DeviceRole.readOnly));
    final container = env.container();
    await waitFor(container, (s) => s is OnlineConnection);

    env.transport.lastHub.emit('DevicePermissionsChanged', [
      {
        'deviceId': 'device',
        'role': 'administrator',
        'effectivePermissions': [PermissionKeys.securityManageDevices],
      },
    ]);

    final online = container.read(agentConnectionControllerProvider) as OnlineConnection;
    expect(online.session.info.role, DeviceRole.administrator);
    expect(online.session.info.can(PermissionKeys.securityManageDevices), isTrue);
  });
}
