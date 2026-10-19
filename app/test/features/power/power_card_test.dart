import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:my_remote_controll/core/contracts/contracts.dart';

import '../../helpers/dashboard_fixtures.dart';
import '../../helpers/fakes.dart';

Future<void> settle(WidgetTester tester) async {
  for (var i = 0; i < 10; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

void main() {
  late TestEnvironment env;

  setUp(() async {
    env = TestEnvironment();
    addTearDown(env.dispose);
    await env.disableLock();
    await env.seedPcs([testPc('a')], activeId: 'a');
  });

  Future<void> openDashboard(
    WidgetTester tester, {
    Map<String, Object?>? power,
    Set<String> permissions = adminPermissions,
  }) async {
    tester.view.physicalSize = const Size(400, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    env.transport.reachable('10.0.0.1', testInfo(permissions: permissions));
    env.transport.hubResponses['SubscribeDashboard'] = (_) => snapshot(1, [pcCard(), power ?? powerCard()]);
    // Resposta padrão de sucesso, como o agente devolve (os testes de erro trocam antes de abrir o painel).
    env.transport.hubResponses.putIfAbsent(
      'PowerAction',
      () =>
          (arguments) => ok({
            'action': (arguments.single as Map<String, Object?>)['action'],
            'executesAt': '2026-10-18T12:00:01Z',
          }),
    );
    await tester.pumpWidget(env.app());
    await settle(tester);
  }

  List<Map<String, Object?>> powerRequests() => [
    for (final call in env.transport.lastHub.calls('PowerAction')) call.single as Map<String, Object?>,
  ];

  /// Confirma a folha: deslizando (desligar, reiniciar, hibernar) ou pelo botão (bloquear, suspender).
  Future<void> confirmSheet(WidgetTester tester, {required bool slide}) async {
    if (slide) {
      await tester.drag(find.byKey(const Key('powerSlide')), const Offset(1000, 0));
    } else {
      await tester.tap(find.byKey(const Key('powerConfirm')));
    }

    await settle(tester);
  }

  group('Card de energia', () {
    testWidgets(
      'ac5_Dado que confirmo Desligar, Então a confirmação avisa que o PC só poderá ser ligado presencialmente',
      (tester) async {
        await openDashboard(tester);

        await tester.tap(find.byKey(const Key('power-shutdown')));
        await settle(tester);

        expect(find.byKey(const Key('shutdownWarning')), findsOneWidget);
        expect(find.textContaining('ligado presencialmente'), findsOneWidget);
        expect(find.byKey(const Key('useRestart')), findsOneWidget);
        expect(find.byKey(const Key('useLock')), findsOneWidget);
      },
    );

    testWidgets('Dado o aviso de Desligar, Quando escolho Bloquear em vez disso, Então a folha troca para Bloquear', (
      tester,
    ) async {
      await openDashboard(tester);

      await tester.tap(find.byKey(const Key('power-shutdown')));
      await settle(tester);
      await tester.tap(find.byKey(const Key('useLock')));
      await settle(tester);
      await confirmSheet(tester, slide: false);

      expect(powerRequests().single['action'], 'lock');
    });

    testWidgets('Dado Desligar com atraso de 1 min e forçar, Quando deslizo, Então o pedido leva atraso e forçar', (
      tester,
    ) async {
      await openDashboard(tester);

      await tester.tap(find.byKey(const Key('power-shutdown')));
      await settle(tester);
      await tester.tap(find.byKey(const Key('delay-60')));
      await tester.tap(find.byKey(const Key('powerForce')));
      await tester.pump();
      await confirmSheet(tester, slide: true);

      expect(powerRequests().single, {
        'action': 'shutdown',
        'delaySeconds': 60,
        'force': true,
        'acknowledgeActiveWork': false,
        'localConfirmation': false,
      });
    });

    testWidgets('ac8_Dado uma ação pendente, Quando peço outra, Então ofereço Substituir: cancela e reenvia', (
      tester,
    ) async {
      var attempts = 0;
      env.transport.hubResponses['PowerAction'] = (_) => attempts++ == 0
          ? failure(ErrorCodes.powerAlreadyScheduled)
          : ok({'action': 'lock', 'executesAt': '2026-10-18T12:00:01Z'});
      await openDashboard(tester);

      await tester.tap(find.byKey(const Key('power-lock')));
      await settle(tester);
      await confirmSheet(tester, slide: false);
      expect(find.text('Substituir'), findsOneWidget);
      await tester.tap(find.byKey(const Key('powerDialogConfirm')));
      await settle(tester);

      expect(env.transport.lastHub.calls('CancelPowerAction'), hasLength(1));
      expect(powerRequests(), hasLength(2));
    });

    testWidgets('ac3_Dado trabalho ativo, Quando peço desligar, Então exijo confirmação extra e reenvio com o aceite', (
      tester,
    ) async {
      var attempts = 0;
      env.transport.hubResponses['PowerAction'] = (_) => attempts++ == 0
          ? failure(
              ErrorCodes.powerActiveWork,
              details: [
                {'kind': 'claudeSession', 'description': 'Projeto api: Claude trabalhando'},
              ],
            )
          : ok({'action': 'shutdown', 'executesAt': '2026-10-18T12:00:01Z'});
      await openDashboard(tester);

      await tester.tap(find.byKey(const Key('power-shutdown')));
      await settle(tester);
      await confirmSheet(tester, slide: true);
      expect(find.text('• Projeto api: Claude trabalhando'), findsOneWidget);
      await tester.tap(find.byKey(const Key('powerDialogConfirm')));
      await settle(tester);

      final requests = powerRequests();
      expect(requests, hasLength(2));
      expect(requests.first['acknowledgeActiveWork'], isFalse);
      expect(requests.last['acknowledgeActiveWork'], isTrue);
    });

    testWidgets('Dado o PC sem suspensão, Então Suspender aparece desabilitado; sem power.shutdown, Desligar some', (
      tester,
    ) async {
      await openDashboard(
        tester,
        power: powerCard(actions: const ['power.lock']),
        permissions: const {PermissionKeys.statusView, PermissionKeys.powerBasic},
      );

      final sleep = tester.widget<IconButton>(find.byKey(const Key('power-sleep')));
      expect(sleep.onPressed, isNull);
      expect(find.byKey(const Key('power-lock')), findsOneWidget);
      expect(find.byKey(const Key('power-shutdown')), findsNothing);
      expect(find.byKey(const Key('power-restart')), findsNothing);
    });

    testWidgets('Dado uma contagem ativa, Então o card mostra o tempo restante e Cancelar chama o agente', (
      tester,
    ) async {
      final executesAt = env.clock.now.toUtc().add(const Duration(minutes: 5));
      await openDashboard(
        tester,
        power: powerCard(
          pending: {
            'action': 'shutdown',
            'executesAt': executesAt.toIso8601String(),
            'force': false,
            'requestedByDeviceName': 'Pixel',
            'updatePending': true,
          },
          actions: const ['power.lock', 'power.cancel'],
        ),
      );

      expect(find.text('Desliga em 5:00'), findsOneWidget);
      expect(find.textContaining('Pedido por Pixel'), findsOneWidget);
      await tester.tap(find.byKey(const Key('powerCancel')));
      await settle(tester);

      expect(env.transport.lastHub.calls('CancelPowerAction'), hasLength(1));
    });

    testWidgets('Dado hibernação desativada, Quando o agente recusa, Então a mensagem é clara', (tester) async {
      env.transport.hubResponses['PowerAction'] = (_) => failure(ErrorCodes.powerHibernateUnavailable);
      await openDashboard(tester);

      await tester.tap(find.byKey(const Key('power-hibernate')));
      await settle(tester);
      await confirmSheet(tester, slide: true);

      expect(find.text('A hibernação está desativada neste PC.'), findsOneWidget);
    });
  });
}
