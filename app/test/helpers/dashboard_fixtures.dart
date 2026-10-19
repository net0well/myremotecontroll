import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable;
import 'package:my_remote_controll/core/contracts/contracts.dart';

/// JSON dos contratos do painel, como o agente manda.
Map<String, Object?> cardJson(String id, Map<String, Object?> data, {List<String> actions = const []}) => {
  'cardId': id,
  'kind': id,
  'data': data,
  'availableActions': actions,
};

Map<String, Object?> cpuCard(double usage, {List<double> cores = const [10, 20]}) =>
    cardJson('cpu', {'usagePercent': usage, 'coreUsagePercent': cores, 'clockMhz': 3600});

Map<String, Object?> memoryCard(int used, {int total = 32 * 1024 * 1024 * 1024}) =>
    cardJson('memory', {'usedBytes': used, 'totalBytes': total});

Map<String, Object?> networkCard(int down, int up) =>
    cardJson('network', {'downBytesPerSecond': down, 'upBytesPerSecond': up, 'interfaces': <Object>[]});

Map<String, Object?> disksCard() => cardJson('disks', {
  'disks': [
    {'drive': 'C:', 'usedBytes': 400, 'totalBytes': 500, 'readBytesPerSecond': 10, 'writeBytesPerSecond': 20},
  ],
});

Map<String, Object?> pcCard({String hostname = 'PC-TESTE'}) => cardJson('pc', {
  'hostname': hostname,
  'osName': 'Windows 11 Pro',
  'osVersion': '25H2 (build 26200.9711)',
  'bootedAt': '2026-10-18T10:00:00Z',
  'userName': r'PC-TESTE\usuario',
  'addresses': [
    {'interfaceName': 'Ethernet', 'kind': 'ethernet', 'address': '192.168.1.3'},
  ],
  'onAcPower': true,
});

/// Card de energia; [pending] = JSON de `PowerCountdownDto`.
Map<String, Object?> powerCard({
  bool sleepAvailable = true,
  bool hibernateAvailable = true,
  Map<String, Object?>? pending,
  List<String> actions = const ['power.lock', 'power.sleep', 'power.restart', 'power.hibernate', 'power.shutdown'],
}) => cardJson('power', {
  'sleepAvailable': sleepAvailable,
  'hibernateAvailable': hibernateAvailable,
  'updatePending': false,
  'pending': ?pending,
}, actions: actions);

Map<String, Object?> ok([Object? data]) => {'ok': true, 'data': data};

Map<String, Object?> failure(String code, {String message = '', Object? details}) => {
  'ok': false,
  'error': {'code': code, 'message': message, 'details': ?details},
};

Map<String, Object?> snapshot(int version, List<Map<String, Object?>> cards) =>
    ok({'version': version, 'at': '2026-10-18T12:00:00Z', 'cards': cards});

/// Argumentos do evento `DashboardDelta(version, patches)`; card nulo = removido.
List<Object?> delta(int version, Map<String, Map<String, Object?>?> patches) => [
  version,
  [
    for (final MapEntry(:key, :value) in patches.entries) {'cardId': key, 'card': value},
  ],
];

/// Todas as permissões de um administrador relevantes ao painel.
const adminPermissions = {
  PermissionKeys.statusView,
  PermissionKeys.monitorView,
  PermissionKeys.powerBasic,
  PermissionKeys.powerShutdown,
};

/// Espera o valor de um provider satisfazer [test] (sem `Future.delayed`, como pede o flutter-tester).
Future<T> waitFor<T>(
  ProviderContainer container,
  ProviderListenable<T> provider,
  bool Function(T value) test, {
  Duration timeout = const Duration(seconds: 3),
}) {
  final current = container.read(provider);
  if (test(current)) {
    return Future.value(current);
  }

  final completer = Completer<T>();
  final subscription = container.listen(provider, (_, next) {
    if (test(next) && !completer.isCompleted) {
      completer.complete(next);
    }
  });
  return completer.future.timeout(timeout).whenComplete(subscription.close);
}
