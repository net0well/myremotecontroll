import 'package:flutter/widget_previews.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../../core/contracts/contracts.dart';
import '../../../core/network/agent_api.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/network/agent_hub.dart';
import '../../../core/network/agent_transport.dart';
import '../../../core/network/pin_registry.dart';
import '../../../core/providers.dart';
import '../../../core/storage/paired_pc.dart';
import '../../../core/widgets/previews.dart';
import '../../security/logic/security_controllers.dart';
import '../data/dashboard_models.dart';
import '../logic/dashboard_controller.dart';
import 'dashboard_cards.dart';

// Previews dos cards do painel (spec 007) nos estados normal, atenção, crítico, carregando, sem permissão e offline.
// O estado vem de providers sobrescritos: nenhum PC de verdade é necessário.

final _now = DateTime.utc(2026, 10, 18, 12);

const _allPermissions = {
  PermissionKeys.statusView,
  PermissionKeys.monitorView,
  PermissionKeys.powerBasic,
  PermissionKeys.powerShutdown,
};

const _previewPc = PairedPc(
  id: 'preview',
  name: 'NETO',
  hosts: ['192.168.1.3'],
  port: 47800,
  pin: 'preview',
  deviceId: 'preview',
  role: DeviceRole.administrator,
);

/// Hub que não conecta a nada: só para montar uma sessão "online" nos previews.
class _PreviewHub implements AgentHub {
  @override
  Future<void> start() async {}

  @override
  Future<void> stop() async {}

  @override
  Stream<List<Object?>> events(String event) => const Stream.empty();

  @override
  Future<HubResult<T>> invoke<T>(String method, List<Object> arguments, T Function(Object? data) parse) async =>
      const HubResult.failure('preview', 'Sem PC nos previews.');
}

AgentConnection _online() => OnlineConnection(
  AgentSession(
    pc: _previewPc,
    host: '192.168.1.3',
    info: const AgentInfo(apiVersion: '1', requiresPairing: false, permissions: _allPermissions),
    api: AgentApi(host: '192.168.1.3', port: 47800, pins: PinRegistry()),
    hub: _PreviewHub(),
  ),
  _now,
);

AgentConnection _offline() =>
    ReconnectingConnection(_previewPc, offlineSince: _now.subtract(const Duration(minutes: 3)), attempt: 2);

CardState _card(String id, Map<String, dynamic> data, {Set<String> actions = const {}}) =>
    CardState(cardId: id, kind: id, data: data, availableActions: actions);

List<double> _series(double base) => [for (var i = 0; i < 60; i++) base + (i % 7) * 3 - (i % 5) * 2];

Widget _scope(
  Widget child, {
  Map<String, CardState> cards = const {},
  bool live = true,
  AgentConnection? connection,
  Set<String> permissions = _allPermissions,
}) => ProviderScope(
  overrides: [
    dashboardControllerProvider.overrideWithBuild(
      (ref, _) => DashboardState(
        pcId: 'preview',
        cards: cards,
        history: {
          HistorySeries.cpu: _series(40),
          HistorySeries.memory: _series(55),
          HistorySeries.networkDown: _series(300000),
          HistorySeries.gpu: _series(20),
        },
        version: 1,
        live: live,
      ),
    ),
    agentConnectionControllerProvider.overrideWithBuild((ref, _) => connection ?? _online()),
    currentPermissionsProvider.overrideWithValue(permissions),
    clockProvider.overrideWithValue(() => _now),
  ],
  child: child,
);

CardState _cpu(double usage) =>
    _card(DashboardCards.cpu, {'usagePercent': usage, 'coreUsagePercent': List.filled(24, usage), 'clockMhz': 4200});

@Preview(group: 'Card CPU', name: 'Normal', wrapper: darkPreviewWrapper)
Widget cpuCardNormal() => _scope(const CpuCard(), cards: {DashboardCards.cpu: _cpu(42)});

@Preview(group: 'Card CPU', name: 'Atenção', wrapper: darkPreviewWrapper)
Widget cpuCardAttention() => _scope(const CpuCard(), cards: {DashboardCards.cpu: _cpu(81)});

@Preview(group: 'Card CPU', name: 'Crítico', wrapper: darkPreviewWrapper)
Widget cpuCardCritical() => _scope(const CpuCard(), cards: {DashboardCards.cpu: _cpu(97)});

@Preview(group: 'Card CPU', name: 'Carregando', wrapper: darkPreviewWrapper)
Widget cpuCardLoading() => _scope(const CpuCard());

@Preview(group: 'Card CPU', name: 'Sem permissão', wrapper: darkPreviewWrapper)
Widget cpuCardNoPermission() => _scope(const CpuCard(), permissions: const {PermissionKeys.statusView});

@Preview(group: 'Card CPU', name: 'Offline', wrapper: darkPreviewWrapper)
Widget cpuCardOffline() =>
    _scope(const CpuCard(), cards: {DashboardCards.cpu: _cpu(42)}, live: false, connection: _offline());

@Preview(group: 'Card CPU', name: 'Contador indisponível', wrapper: darkPreviewWrapper)
Widget cpuCardUnavailable() =>
    _scope(const CpuCard(), cards: {DashboardCards.cpu: _card(DashboardCards.cpu, const {})});

@Preview(group: 'Cards de métricas', name: 'Memória', wrapper: darkPreviewWrapper)
Widget memoryCardNormal() => _scope(
  const MemoryCard(),
  cards: {
    DashboardCards.memory: _card(DashboardCards.memory, {'usedBytes': 11811160064, 'totalBytes': 34359738368}),
  },
);

@Preview(group: 'Cards de métricas', name: 'Rede', wrapper: darkPreviewWrapper)
Widget networkCardNormal() => _scope(
  const NetworkCard(),
  cards: {
    DashboardCards.network: _card(DashboardCards.network, {'downBytesPerSecond': 2456000, 'upBytesPerSecond': 312000}),
  },
);

@Preview(group: 'Cards de métricas', name: 'Discos (crítico)', wrapper: darkPreviewWrapper)
Widget disksCardCritical() => _scope(
  const DisksCard(),
  cards: {
    DashboardCards.disks: _card(DashboardCards.disks, {
      'disks': [
        {'drive': 'C:', 'usedBytes': 485000000000, 'totalBytes': 499000000000},
        {'drive': 'D:', 'label': 'Dados', 'usedBytes': 1200000000000, 'totalBytes': 2000000000000},
        {'drive': 'E:', 'label': 'Jogos', 'usedBytes': 500000000000, 'totalBytes': 1000000000000},
      ],
    }),
  },
);

@Preview(group: 'Cards de métricas', name: 'GPU', wrapper: darkPreviewWrapper)
Widget gpuCardNormal() => _scope(
  const GpuCard(),
  cards: {
    DashboardCards.gpu: _card(DashboardCards.gpu, {
      'name': 'AMD Radeon RX 7600',
      'usagePercent': 23,
      'dedicatedUsedBytes': 805306368,
    }),
  },
);

@Preview(group: 'Cards de métricas', name: 'Bateria (atenção)', wrapper: darkPreviewWrapper)
Widget batteryCardAttention() => _scope(
  const BatteryCard(),
  cards: {
    DashboardCards.battery: _card(DashboardCards.battery, {'percent': 18, 'charging': false, 'secondsRemaining': 2400}),
  },
);

CardState _power({Map<String, dynamic>? pending, Set<String>? actions}) => _card(
  DashboardCards.power,
  {'sleepAvailable': false, 'hibernateAvailable': true, 'updatePending': false, 'pending': ?pending},
  actions:
      actions ??
      {PowerCardActions.lock, PowerCardActions.restart, PowerCardActions.hibernate, PowerCardActions.shutdown},
);

@Preview(group: 'Card Energia', name: 'Ações (Suspender indisponível)', wrapper: darkPreviewWrapper)
Widget powerCardActions() => _scope(const PowerCard(), cards: {DashboardCards.power: _power()});

@Preview(group: 'Card Energia', name: 'Contagem ativa', wrapper: darkPreviewWrapper)
Widget powerCardCountdown() => _scope(
  const PowerCard(),
  cards: {
    DashboardCards.power: _power(
      pending: {
        'action': 'shutdown',
        'executesAt': _now.add(const Duration(minutes: 4, seconds: 58)).toIso8601String(),
        'force': false,
        'requestedByDeviceName': 'Pixel 8',
        'updatePending': true,
      },
      actions: {PowerCardActions.lock, PowerCardActions.cancel},
    ),
  },
);

@Preview(group: 'Card Energia', name: 'Somente leitura', wrapper: darkPreviewWrapper)
Widget powerCardReadOnly() => _scope(
  const PowerCard(),
  cards: {DashboardCards.power: _power(actions: const {})},
  permissions: const {PermissionKeys.statusView, PermissionKeys.monitorView},
);

CardState _pc() => _card(DashboardCards.pc, {
  'hostname': 'NETO',
  'osName': 'Windows 11 Pro',
  'osVersion': '25H2 (build 26200.9711)',
  'bootedAt': _now.subtract(const Duration(hours: 5)).toIso8601String(),
  'userName': r'NETO\usuario',
  'addresses': [
    {'interfaceName': 'Ethernet', 'kind': 'ethernet', 'address': '192.168.1.3'},
  ],
  'onAcPower': true,
  'powerPlan': 'Equilibrado',
});

@Preview(group: 'Card PC', name: 'Online', wrapper: darkPreviewWrapper)
Widget pcCardOnline() => _scope(const PcCard(), cards: {DashboardCards.pc: _pc()});

@Preview(group: 'Card PC', name: 'Offline', wrapper: darkPreviewWrapper)
Widget pcCardOffline() =>
    _scope(const PcCard(), cards: {DashboardCards.pc: _pc()}, live: false, connection: _offline());

@Preview(group: 'Card PC', name: 'Claro', wrapper: lightPreviewWrapper)
Widget pcCardLight() => _scope(const PcCard(), cards: {DashboardCards.pc: _pc()});
