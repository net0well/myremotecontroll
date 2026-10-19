import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/core/network/agent_connection.dart';
import 'package:my_remote_controll/features/dashboard/data/dashboard_models.dart';
import 'package:my_remote_controll/features/dashboard/logic/dashboard_controller.dart';

import '../../helpers/dashboard_fixtures.dart';
import '../../helpers/fakes.dart';

double? cpuOf(DashboardState state) => CpuData.fromJson(state.cards['cpu']?.data ?? const {}).usagePercent;

void main() {
  late TestEnvironment env;

  setUp(() async {
    env = TestEnvironment();
    addTearDown(env.dispose);
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1', testInfo(permissions: adminPermissions));
    env.transport.hubResponses['SubscribeDashboard'] = (_) => snapshot(1, [cpuCard(10), memoryCard(1024)]);
  });

  group('DashboardController', () {
    test('Dado o PC online, Quando o painel abre, Então assina com 1 s e aplica o snapshot', () async {
      final container = env.container();

      final state = await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

      final subscribe = env.transport.lastHub.calls('SubscribeDashboard').single.single as Map<String, Object?>;
      expect(subscribe['metricsIntervalMs'], dashboardIntervalMs);
      expect(subscribe['lowDataMode'], isFalse);
      expect(state.live, isTrue);
      expect(cpuOf(state), 10);
    });

    test('Dado dados móveis ou economia de bateria, Quando assina, Então pede 3 s e dados reduzidos', () async {
      env.dataSaver.constrained = true;
      final container = env.container();

      await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

      final subscribe = env.transport.lastHub.calls('SubscribeDashboard').single.single as Map<String, Object?>;
      expect(subscribe['metricsIntervalMs'], dashboardConstrainedIntervalMs);
      expect(subscribe['lowDataMode'], isTrue);
    });

    test('Dado deltas em sequência, Quando chegam, Então trocam só os cards do patch e acumulam a série', () async {
      final container = env.container();
      final before = await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

      env.transport.lastHub.emit('DashboardDelta', delta(2, {'cpu': cpuCard(30)}));
      final after = await waitFor(container, dashboardControllerProvider, (s) => s.version == 2);

      expect(cpuOf(after), 30);
      expect(identical(after.cards['memory'], before.cards['memory']), isTrue, reason: 'memória não mudou');
      expect(after.history[HistorySeries.cpu], [10, 30]);
    });

    test(
      'ac3_Dado um delta com versão fora de sequência, Quando ele chega, Então o app pede um novo snapshot',
      () async {
        env.transport.hubResponses['RequestDashboardSnapshot'] = (_) => snapshot(5, [cpuCard(55)]);
        final container = env.container();
        await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

        env.transport.lastHub.emit('DashboardDelta', delta(3, {'cpu': cpuCard(99)}));
        final state = await waitFor(container, dashboardControllerProvider, (s) => s.version == 5);

        expect(env.transport.lastHub.calls('RequestDashboardSnapshot'), hasLength(1));
        expect(cpuOf(state), 55, reason: 'o delta 3 (fora de sequência) não pode ser aplicado');
        expect(state.cards.containsKey('memory'), isFalse, reason: 'o snapshot substitui o estado inteiro');
      },
    );

    test(
      'Dado um snapshot em andamento, Quando chegam deltas no meio, Então só os mais novos são aplicados depois',
      () async {
        final pendingSnapshot = Completer<Object?>();
        env.transport.hubResponses['RequestDashboardSnapshot'] = (_) => pendingSnapshot.future;
        final container = env.container();
        await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);
        final hub = env.transport.lastHub;

        hub.emit('DashboardDelta', delta(4, {'cpu': cpuCard(40)}));
        await waitFor(container, dashboardControllerProvider, (_) => hub.calls('RequestDashboardSnapshot').isNotEmpty);
        hub
          ..emit('DashboardDelta', delta(5, {'cpu': cpuCard(50)}))
          ..emit('DashboardDelta', delta(7, {'cpu': cpuCard(70)}));
        pendingSnapshot.complete(snapshot(6, [cpuCard(60)]));

        final state = await waitFor(container, dashboardControllerProvider, (s) => s.version == 7);
        expect(cpuOf(state), 70);
      },
    );

    test(
      'ac4_Dado o PC offline, Quando a conexão cai, Então o último estado continua e o painel deixa de estar ao vivo',
      () async {
        final container = env.container();
        await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

        env.transport.failing('10.0.0.1', AgentFailureKind.unreachable);
        env.transport.lastHub.drop();
        await waitFor(container, agentConnectionControllerProvider, (c) => c is ReconnectingConnection);
        final state = await waitFor(container, dashboardControllerProvider, (s) => !s.live);

        expect(cpuOf(state), 10);
      },
    );

    test('Dado o app em segundo plano, Quando volta, Então cancela a assinatura e assina de novo', () async {
      final container = env.container();
      await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);
      final controller = container.read(dashboardControllerProvider.notifier);

      await controller.pause();
      expect(container.read(dashboardControllerProvider).live, isFalse);
      controller.resume();
      await waitFor(container, dashboardControllerProvider, (s) => s.live);

      final hub = env.transport.lastHub;
      expect(hub.calls('UnsubscribeDashboard'), hasLength(1));
      expect(hub.calls('SubscribeDashboard'), hasLength(2));
    });

    test('Dado a rede mudou para dados móveis, Quando o app percebe, Então assina de novo com 3 s', () async {
      final container = env.container();
      await waitFor(container, dashboardControllerProvider, (s) => s.version == 1);

      env.dataSaver
        ..constrained = true
        ..networkChanged();
      await waitFor(
        container,
        dashboardControllerProvider,
        (_) => env.transport.lastHub.calls('SubscribeDashboard').length == 2,
      );

      final last = env.transport.lastHub.calls('SubscribeDashboard').last.single as Map<String, Object?>;
      expect(last['metricsIntervalMs'], dashboardConstrainedIntervalMs);
    });
  });
}
