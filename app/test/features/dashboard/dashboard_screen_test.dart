import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/features/dashboard/ui/dashboard_cards.dart';

import '../../helpers/dashboard_fixtures.dart';
import '../../helpers/fakes.dart';

/// O indicador "ao vivo" pulsa sempre, então `pumpAndSettle` não termina: avança o tempo em passos.
Future<void> settle(WidgetTester tester) async {
  for (var i = 0; i < 10; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

Finder inCard(String cardId, Finder matching) =>
    find.descendant(of: find.byKey(Key('card-$cardId')), matching: matching);

void main() {
  late TestEnvironment env;

  setUp(() async {
    env = TestEnvironment();
    addTearDown(env.dispose);
    await env.disableLock();
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1', testInfo(permissions: adminPermissions));
    env.transport.hubResponses['SubscribeDashboard'] = (_) => snapshot(1, [
      pcCard(),
      powerCard(),
      cpuCard(10),
      memoryCard(8 * 1024 * 1024 * 1024),
      networkCard(2048, 512),
      disksCard(),
    ]);
  });

  /// Celular por padrão (largura de 400 px lógicos).
  Future<void> openDashboard(WidgetTester tester, {Size size = const Size(400, 900)}) async {
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(env.app());
    await settle(tester);
  }

  testWidgets('ac1_Dado o painel aberto, Quando chega uma métrica nova, Então o card atualiza com transição animada', (
    tester,
  ) async {
    await openDashboard(tester);
    expect(inCard('cpu', find.text('10%')), findsOneWidget);

    env.transport.lastHub.emit('DashboardDelta', delta(2, {'cpu': cpuCard(80)}));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 60));

    expect(inCard('cpu', find.text('80%')), findsNothing, reason: 'no meio da transição');
    expect(inCard('cpu', find.text('10%')), findsNothing);

    await settle(tester);
    expect(inCard('cpu', find.text('80%')), findsOneWidget);
  });

  testWidgets('ac4_Dado o PC offline, Então o painel mostra offline, há quanto tempo e o último estado conhecido', (
    tester,
  ) async {
    await openDashboard(tester);

    env.transport.failing('10.0.0.1', AgentFailureKind.unreachable);
    env.transport.lastHub.drop();
    await settle(tester);

    expect(find.byKey(const Key('offlineSince')), findsOneWidget);
    expect(inCard('cpu', find.text('10%')), findsOneWidget, reason: 'último valor conhecido');
  });

  testWidgets('ac5_Dado uma mudança só de CPU, Então apenas o card de CPU é reconstruído', (tester) async {
    await openDashboard(tester);
    DashboardDebug.builds.clear();

    env.transport.lastHub.emit('DashboardDelta', delta(2, {'cpu': cpuCard(35)}));
    await tester.pump();

    expect(DashboardDebug.builds.keys, ['cpu']);
  });

  testWidgets('ac6_Dado um tablet em paisagem, Então o painel mostra pelo menos 3 colunas', (tester) async {
    await openDashboard(tester, size: const Size(1280, 800));

    final firstRow = [
      for (final id in ['pc', 'power', 'cpu']) tester.getTopLeft(find.byKey(Key('card-$id'))),
    ];

    expect(firstRow.map((offset) => offset.dy).toSet(), hasLength(1), reason: 'na mesma linha');
    expect(firstRow.map((offset) => offset.dx).toSet(), hasLength(3), reason: 'em colunas diferentes');
  });

  testWidgets('Dado o celular, Então o painel usa uma coluna e mostra os cards na ordem da spec', (tester) async {
    await openDashboard(tester);

    final pc = tester.getTopLeft(find.byKey(const Key('card-pc')));
    final power = tester.getTopLeft(find.byKey(const Key('card-power')));

    expect(power.dx, pc.dx);
    expect(power.dy, greaterThan(pc.dy));
  });

  testWidgets('Dado um card com dados, Quando toco nele, Então abre o detalhe com os núcleos', (tester) async {
    await openDashboard(tester);

    await tester.tap(find.byKey(const Key('card-cpu')));
    await settle(tester);

    expect(find.text('Núcleo 0'), findsOneWidget);
    expect(find.text('Núcleo 1'), findsOneWidget);
  });
}
