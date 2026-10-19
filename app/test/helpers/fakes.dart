import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:my_remote_controll/app/app.dart';
import 'package:my_remote_controll/core/contracts/contracts.dart';
import 'package:my_remote_controll/core/errors/agent_exception.dart';
import 'package:my_remote_controll/core/network/agent_api.dart';
import 'package:my_remote_controll/core/network/agent_connection.dart';
import 'package:my_remote_controll/core/network/agent_hub.dart';
import 'package:my_remote_controll/core/network/agent_transport.dart';
import 'package:my_remote_controll/core/network/pin_registry.dart';
import 'package:my_remote_controll/core/platform/data_saver.dart';
import 'package:my_remote_controll/core/providers.dart';
import 'package:my_remote_controll/core/security/app_lock.dart';
import 'package:my_remote_controll/core/storage/paired_pc.dart';
import 'package:my_remote_controll/core/storage/paired_pcs.dart';
import 'package:my_remote_controll/core/storage/stores.dart';
import 'package:my_remote_controll/features/dashboard/logic/dashboard_controller.dart';
import 'package:my_remote_controll/features/pairing/ui/pairing_screen.dart';

PairedPc testPc(String id, {List<String> hosts = const ['10.0.0.1'], String name = 'PC de teste'}) => PairedPc(
  id: id,
  name: name,
  hosts: hosts,
  port: 47800,
  pin: id,
  deviceId: 'device-$id',
  role: DeviceRole.administrator,
);

AgentInfo testInfo({Set<String> permissions = const {}, DeviceRole role = DeviceRole.administrator}) => AgentInfo(
  apiVersion: '1',
  requiresPairing: false,
  pcName: 'PC de teste',
  deviceId: 'device',
  role: role,
  permissions: permissions,
);

/// Resposta roteirizada de um método do Hub (JSON de `HubResponse`); pode ser assíncrona.
typedef HubResponder = FutureOr<Object?> Function(List<Object> arguments);

/// Hub falso: guarda os eventos registrados e as chamadas, e permite simular eventos do servidor e queda da conexão.
class FakeHub implements AgentHub {
  FakeHub(this.onClosed, [Map<String, HubResponder>? responses]) : responses = responses ?? {};

  final void Function(Object? error) onClosed;
  final Map<String, StreamController<List<Object?>>> _events = {};

  /// Compartilhado com o [FakeTransport]: vale também para os hubs das próximas conexões.
  final Map<String, HubResponder> responses;
  final List<(String, List<Object>)> invocations = [];
  bool stopped = false;

  List<List<Object>> calls(String method) => [
    for (final (name, arguments) in invocations)
      if (name == method) arguments,
  ];

  /// Eventos que alguém assinou.
  Iterable<String> get subscribedEvents => _events.entries.where((e) => e.value.hasListener).map((e) => e.key);

  void emit(String event, List<Object?> arguments) => _events[event]?.add(arguments);

  void drop() => onClosed(Exception('conexão perdida'));

  @override
  Future<void> start() async {}

  @override
  Future<void> stop() async => stopped = true;

  @override
  Stream<List<Object?>> events(String event) =>
      _events.putIfAbsent(event, () => StreamController<List<Object?>>.broadcast(sync: true)).stream;

  @override
  Future<HubResult<T>> invoke<T>(String method, List<Object> arguments, T Function(Object? data) parse) async {
    invocations.add((method, arguments));
    final json = await responses[method]?.call(arguments) ?? {'ok': true, 'data': null};
    return HubResult.fromJson(jsonDecode(jsonEncode(json)), parse);
  }
}

/// Transporte falso: cada host responde com um [AgentInfo] ou falha com o tipo configurado.
class FakeTransport implements AgentTransport {
  final Map<String, Object> hostBehavior = {};
  final List<FakeHub> hubs = [];

  /// Respostas do Hub para todas as conexões. O painel abre em quase todo teste, então há um snapshot vazio padrão.
  final Map<String, HubResponder> hubResponses = {
    'SubscribeDashboard': (_) => _emptySnapshot,
    'RequestDashboardSnapshot': (_) => _emptySnapshot,
  };

  static const _emptySnapshot = {
    'ok': true,
    'data': {'version': 1, 'at': '2026-10-18T12:00:00Z', 'cards': <Object>[]},
  };
  Duration probeDelay = Duration.zero;
  int probes = 0;

  FakeHub get lastHub => hubs.last;

  void reachable(String host, [AgentInfo? info]) => hostBehavior[host] = info ?? testInfo();

  void failing(String host, AgentFailureKind kind, {String? code}) =>
      hostBehavior[host] = AgentException(kind, code: code);

  @override
  Future<AgentInfo> probe(PairedPc pc, String host, String token) async {
    probes++;
    if (probeDelay > Duration.zero) {
      await Future<void>.delayed(probeDelay);
    }
    final behavior = hostBehavior[host] ?? const AgentException(AgentFailureKind.unreachable);
    if (behavior is AgentException) {
      throw behavior;
    }

    return behavior as AgentInfo;
  }

  @override
  Future<AgentSession> open(
    PairedPc pc,
    String host,
    String token,
    AgentInfo info, {
    required void Function(Object? error) onClosed,
  }) async {
    final hub = FakeHub(onClosed, hubResponses);
    hubs.add(hub);
    return AgentSession(
      pc: pc,
      host: host,
      info: info,
      api: AgentApi(host: host, port: pc.port, pins: PinRegistry()),
      hub: hub,
    );
  }

  @override
  Future<String> rotateToken(PairedPc pc, String host, String token) async => 'rotated-$token';
}

class FakeAuthenticator implements DeviceAuthenticator {
  bool available = true;
  bool accept = true;
  int prompts = 0;

  @override
  Future<bool> isAvailable() async => available;

  @override
  Future<bool> authenticate(String reason) async {
    prompts++;
    return accept;
  }
}

/// Relógio controlável para testes de tempo (bloqueio do app, "offline há X").
class TestClock {
  DateTime now = DateTime(2026, 10, 18, 12);

  DateTime call() => now;

  void advance(Duration duration) => now = now.add(duration);
}

/// Economia de dados controlável (dados móveis / economia de bateria).
class FakeDataSaver implements DataSaver {
  bool constrained = false;
  final _changes = StreamController<void>.broadcast();

  void networkChanged() => _changes.add(null);

  @override
  Future<bool> isConstrained() async => constrained;

  @override
  Stream<void> get changes => _changes.stream;

  Future<void> dispose() => _changes.close();
}

class TestEnvironment {
  TestEnvironment();

  final keyValues = MemoryKeyValueStore();
  final secrets = MemorySecretStore();
  final transport = FakeTransport();
  final authenticator = FakeAuthenticator();
  final clock = TestClock();
  final network = StreamController<void>.broadcast();
  final dataSaver = FakeDataSaver();

  Future<void> dispose() async {
    await network.close();
    await dataSaver.dispose();
  }

  /// Grava PCs como se já estivessem pareados (com token).
  Future<void> seedPcs(List<PairedPc> pcs, {String? activeId}) async {
    await keyValues.setString('paired_pcs.v1', jsonEncode(pcs.map((pc) => pc.toJson()).toList()));
    if (activeId != null) {
      await keyValues.setString('active_pc.v1', activeId);
    }

    for (final pc in pcs) {
      await secrets.write(PairedPcs.tokenKey(pc.id), 'token-${pc.id}');
    }
  }

  /// Desliga o bloqueio por biometria (para testes que não tratam dele).
  Future<void> disableLock() => keyValues.setString('app_settings.v1', jsonEncode({'biometricLock': false}));

  List<Override> get overrides => [
    keyValueStoreProvider.overrideWithValue(keyValues),
    secretStoreProvider.overrideWithValue(secrets),
    pinRegistryProvider.overrideWithValue(PinRegistry()),
    agentTransportProvider.overrideWithValue(transport),
    deviceAuthenticatorProvider.overrideWithValue(authenticator),
    clockProvider.overrideWithValue(clock.call),
    networkAvailableProvider.overrideWith((ref) => network.stream),
    reconnectDelaysProvider.overrideWithValue(const [Duration(minutes: 5)]),
    qrScannerEnabledProvider.overrideWithValue(false),
    dataSaverProvider.overrideWithValue(dataSaver),
  ];

  /// Container com descarte automático no fim do teste (padrão `createContainer` do flutter-tester).
  ProviderContainer container() {
    final container = ProviderContainer(overrides: overrides);
    addTearDown(container.dispose);
    return container;
  }

  Widget app() => ProviderScope(overrides: overrides, child: const MyRemoteControllApp());
}
