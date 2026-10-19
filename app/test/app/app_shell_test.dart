import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:my_remote_controll/app/app.dart';
import 'package:my_remote_controll/core/contracts/contracts.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/core/widgets/charts.dart';
import 'package:my_remote_controll/core/widgets/live_indicators.dart';
import 'package:my_remote_controll/features/pairing/data/pairing_link.dart';
import 'package:my_remote_controll/features/pairing/data/pairing_service.dart';
import 'package:my_remote_controll/features/pairing/logic/pairing_controller.dart';
import 'package:my_remote_controll/l10n/app_localizations.dart';

import '../helpers/fakes.dart';

const _validLink =
    'mrc://pair?v=1&h=10.0.0.1&p=47800&c=AAAAAAAAAAAAAAAAAAAAAA&f=q2V1CbPFlOaouT0Ez2evXTKt7GWz25n7UvP3lWn5PXw&n=PC%20Teste';

class _ControlledPairing implements PairingService {
  final completer = Completer<PairingOutcome>();

  @override
  Future<PairingOutcome> pair(PairingLink link, {required String deviceName, required DevicePlatform platform}) =>
      completer.future;
}

Widget _localized(Widget child, {bool reduceMotion = false}) => MediaQuery(
  data: MediaQueryData(disableAnimations: reduceMotion),
  child: MaterialApp(
    locale: const Locale('pt'),
    localizationsDelegates: const [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
    supportedLocales: AppLocalizations.supportedLocales,
    home: Scaffold(body: Center(child: child)),
  ),
);

/// Avança o tempo o bastante para transições de tela; o indicador "ao vivo" pulsa sempre, então pumpAndSettle não termina.
Future<void> settle(WidgetTester tester) async {
  for (var i = 0; i < 10; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

/// Ida e volta do segundo plano na sequência real do sistema (o AppLifecycleListener do painel valida as transições).
void toBackground(WidgetTester tester) {
  for (final state in [AppLifecycleState.inactive, AppLifecycleState.hidden, AppLifecycleState.paused]) {
    tester.binding.handleAppLifecycleStateChanged(state);
  }
}

void toForeground(WidgetTester tester) {
  for (final state in [AppLifecycleState.hidden, AppLifecycleState.inactive, AppLifecycleState.resumed]) {
    tester.binding.handleAppLifecycleStateChanged(state);
  }
}

void main() {
  late TestEnvironment env;

  setUp(() => env = TestEnvironment());

  testWidgets('ac3_com biometria ligada, pede desbloqueio ao abrir e após 5 min em segundo plano', (tester) async {
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1');
    await tester.pumpWidget(env.app());
    await tester.pump();

    expect(find.byType(LockScreen), findsOneWidget);

    await tester.tap(find.byKey(const Key('unlock')));
    await tester.pump();
    expect(find.byType(LockScreen), findsNothing);
    expect(env.authenticator.prompts, 1);

    toBackground(tester);
    env.clock.advance(const Duration(minutes: 4));
    toForeground(tester);
    await tester.pump();
    expect(find.byType(LockScreen), findsNothing, reason: 'menos de 5 min não bloqueia');

    toBackground(tester);
    env.clock.advance(const Duration(minutes: 5));
    toForeground(tester);
    await tester.pump();
    expect(find.byType(LockScreen), findsOneWidget);
  });

  testWidgets('ac4_com o sistema em inglês, os textos continuam em pt-BR', (tester) async {
    tester.platformDispatcher.localesTestValue = const [Locale('en', 'US')];
    addTearDown(tester.platformDispatcher.clearLocalesTestValue);
    await env.disableLock();

    await tester.pumpWidget(env.app());
    await tester.pumpAndSettle();

    expect(find.text('Parear com um PC'), findsOneWidget);
  });

  testWidgets('ac5_token revogado mostra "Aparelho revogado" com opção de reparear', (tester) async {
    await env.disableLock();
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.failing('10.0.0.1', AgentFailureKind.unauthorized, code: ErrorCodes.authRevoked);

    await tester.pumpWidget(env.app());
    await tester.pumpAndSettle();

    expect(find.text('Aparelho revogado'), findsOneWidget);
    expect(find.text('Parear novamente'), findsOneWidget);
  });

  testWidgets('ac6_com "reduzir movimento", o gauge muda na hora e o indicador ao vivo não anima', (tester) async {
    await tester.pumpWidget(_localized(const Gauge(value: 0.2, label: 'CPU'), reduceMotion: true));
    await tester.pumpWidget(_localized(const Gauge(value: 0.8, label: 'CPU'), reduceMotion: true));
    await tester.pump();
    expect(find.text('80%'), findsOneWidget);

    await tester.pumpWidget(_localized(const LiveIndicator(), reduceMotion: true));
    await tester.pump();
    expect(tester.hasRunningAnimations, isFalse);
  });

  testWidgets('sem "reduzir movimento", o gauge anima até o novo valor', (tester) async {
    await tester.pumpWidget(_localized(const Gauge(value: 0.2, label: 'CPU')));
    await tester.pumpAndSettle();
    await tester.pumpWidget(_localized(const Gauge(value: 0.8, label: 'CPU')));
    await tester.pump(const Duration(milliseconds: 50));

    expect(find.text('80%'), findsNothing);
    await tester.pumpAndSettle();
    expect(find.text('80%'), findsOneWidget);
  });

  testWidgets('ac7_aparelho somente leitura não vê Segurança → Aparelhos', (tester) async {
    await env.disableLock();
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable('10.0.0.1', testInfo(role: DeviceRole.readOnly, permissions: {PermissionKeys.statusView}));

    await tester.pumpWidget(env.app());
    await settle(tester);
    await tester.tap(find.byTooltip('Configurações'));
    await settle(tester);

    expect(find.byKey(const Key('settings-devices')), findsNothing);
    expect(find.byKey(const Key('settings-audit')), findsNothing);
  });

  testWidgets('administrador vê Segurança → Aparelhos e Auditoria', (tester) async {
    await env.disableLock();
    await env.seedPcs([testPc('a')], activeId: 'a');
    env.transport.reachable(
      '10.0.0.1',
      testInfo(permissions: {PermissionKeys.securityManageDevices, PermissionKeys.securityViewAudit}),
    );

    await tester.pumpWidget(env.app());
    await settle(tester);
    await tester.tap(find.byTooltip('Configurações'));
    await settle(tester);

    expect(find.byKey(const Key('settings-devices')), findsOneWidget);
    expect(find.byKey(const Key('settings-audit')), findsOneWidget);
  });

  testWidgets('pareamento: aguarda a confirmação no PC e abre o painel ao concluir', (tester) async {
    await env.disableLock();
    final pairing = _ControlledPairing();
    env.transport.reachable('10.0.0.1');
    await tester.pumpWidget(
      ProviderScope(
        overrides: [...env.overrides, pairingServiceProvider.overrideWithValue(pairing)],
        child: const MyRemoteControllApp(),
      ),
    );
    await settle(tester);

    await tester.enterText(find.byKey(const Key('pairing-link')), _validLink);
    await tester.tap(find.byKey(const Key('pairing-start')));
    await tester.pump();
    expect(find.text('Confirme o pareamento na tela do PC…'), findsOneWidget);

    pairing.completer.complete(PairingOutcome(testPc('q2V1', hosts: ['10.0.0.1'], name: 'PC Teste'), 'token'));
    await settle(tester);

    expect(find.text('PC e conexão'), findsOneWidget);
    expect(await env.secrets.read('token:q2V1'), 'token');
  });

  testWidgets('link inválido mostra mensagem clara', (tester) async {
    await env.disableLock();
    await tester.pumpWidget(env.app());
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('pairing-link')), 'https://exemplo.com');
    await tester.tap(find.byKey(const Key('pairing-start')));
    await tester.pump();

    expect(find.text('Este não é um código de pareamento do MyRemoteControll.'), findsOneWidget);
  });
}
