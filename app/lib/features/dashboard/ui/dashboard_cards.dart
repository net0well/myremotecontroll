import 'dart:async';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/contracts/contracts.dart';
import '../../../core/formatting/durations.dart';
import '../../../core/formatting/units.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/charts.dart';
import '../../../core/widgets/dash_card.dart';
import '../../../core/widgets/live_indicators.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../l10n/app_localizations.dart';
import '../../power/ui/power_flow.dart';
import '../../security/logic/security_controllers.dart';
import '../data/dashboard_models.dart';
import '../logic/dashboard_controller.dart';

/// Contagem de builds por card, só em testes (spec 007, AC-5: só o card que mudou reconstrói).
@visibleForTesting
abstract final class DashboardDebug {
  static final builds = <String, int>{};

  static void record(String cardId) {
    assert(() {
      builds.update(cardId, (count) => count + 1, ifAbsent: () => 1);
      return true;
    }(), 'contagem só em debug');
  }
}

/// Limiares de destaque (seção 4.3 do documento base).
Severity usageSeverity(double? percent, {double attention = 75, double critical = 90}) {
  if (percent == null) {
    return Severity.normal;
  }

  return percent >= critical
      ? Severity.critical
      : percent >= attention
      ? Severity.attention
      : Severity.normal;
}

String? severityLabel(AppLocalizations l10n, Severity severity) => switch (severity) {
  Severity.attention => l10n.severityAttention,
  Severity.critical => l10n.severityCritical,
  _ => null,
};

/// Moldura comum: escolhe o estado do card (ao vivo, carregando, sem permissão, offline com o último valor) e
/// assiste só ao provider do próprio card.
class DashboardCardFrame extends ConsumerWidget {
  const DashboardCardFrame({
    super.key,
    required this.cardId,
    required this.title,
    required this.icon,
    required this.readPermission,
    required this.builder,
    this.severity,
  });

  final String cardId;
  final String title;
  final IconData icon;
  final String readPermission;
  final Widget Function(BuildContext context, CardState card) builder;
  final Severity Function(CardState card)? severity;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    DashboardDebug.record(cardId);
    final l10n = AppLocalizations.of(context);
    final card = ref.watch(dashboardCardProvider(cardId));
    final live = ref.watch(dashboardControllerProvider.select((state) => state.live));
    final online = ref.watch(agentConnectionControllerProvider.select((c) => c is OnlineConnection));
    final canRead = ref.watch(currentPermissionsProvider.select((permissions) => permissions.contains(readPermission)));

    final state = switch ((card, online, live)) {
      _ when online && !canRead => DashCardState.noPermission,
      (null, true, _) => DashCardState.loading,
      (null, false, _) => DashCardState.offline,
      (_, _, false) => DashCardState.offline,
      _ => DashCardState.ready,
    };
    final level = card == null || severity == null ? null : severity!(card);

    return DashCard(
      key: Key('card-$cardId'),
      title: title,
      icon: icon,
      state: state,
      severity: level,
      severityLabel: level == null ? null : severityLabel(l10n, level),
      trailing: state == DashCardState.ready ? const LiveIndicator() : null,
      onTap: card == null ? null : () => context.push('/card/$cardId'),
      child: card == null
          ? Text(l10n.valueUnavailable, style: Theme.of(context).textTheme.headlineSmall)
          : builder(context, card),
    );
  }
}

class CpuCard extends ConsumerWidget {
  const CpuCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.cpu,
      title: l10n.cardCpu,
      icon: Icons.memory_rounded,
      readPermission: PermissionKeys.monitorView,
      severity: (card) => usageSeverity(CpuData.fromJson(card.data).usagePercent),
      builder: (context, card) {
        final cpu = CpuData.fromJson(card.data);
        return _GaugeWithDetail(
          value: cpu.usagePercent,
          label: l10n.cardCpu,
          severity: usageSeverity(cpu.usagePercent),
          series: HistorySeries.cpu,
          maxValue: 100,
          detail: cpu.clockMhz == null ? null : l10n.cpuClock(Units.clock(cpu.clockMhz)),
        );
      },
    );
  }
}

class MemoryCard extends ConsumerWidget {
  const MemoryCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.memory,
      title: l10n.cardMemory,
      icon: Icons.developer_board_rounded,
      readPermission: PermissionKeys.monitorView,
      severity: (card) => usageSeverity(_percent(MemoryData.fromJson(card.data).usedFraction), attention: 80),
      builder: (context, card) {
        final memory = MemoryData.fromJson(card.data);
        return _GaugeWithDetail(
          value: _percent(memory.usedFraction),
          label: l10n.cardMemory,
          severity: usageSeverity(_percent(memory.usedFraction), attention: 80),
          series: HistorySeries.memory,
          maxValue: 100,
          detail: l10n.memoryUsage(Units.bytes(memory.usedBytes), Units.bytes(memory.totalBytes)),
        );
      },
    );
  }
}

class GpuCard extends ConsumerWidget {
  const GpuCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.gpu,
      title: l10n.cardGpu,
      icon: Icons.videogame_asset_rounded,
      readPermission: PermissionKeys.monitorView,
      severity: (card) => usageSeverity(GpuData.fromJson(card.data).usagePercent),
      builder: (context, card) {
        final gpu = GpuData.fromJson(card.data);
        final vram = gpu.dedicatedUsedBytes == null ? null : l10n.gpuVram(Units.bytes(gpu.dedicatedUsedBytes));
        return _GaugeWithDetail(
          value: gpu.usagePercent,
          label: l10n.cardGpu,
          severity: usageSeverity(gpu.usagePercent),
          series: HistorySeries.gpu,
          maxValue: 100,
          detail: [?gpu.name, ?vram].join('\n'),
        );
      },
    );
  }
}

class NetworkCard extends ConsumerWidget {
  const NetworkCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.network,
      title: l10n.cardNetwork,
      icon: Icons.swap_vert_rounded,
      readPermission: PermissionKeys.monitorView,
      builder: (context, card) {
        final network = NetworkData.fromJson(card.data);
        final theme = Theme.of(context);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l10n.networkDown(Units.rate(network.down)), style: theme.textTheme.titleLarge),
            Text(l10n.networkUp(Units.rate(network.up)), style: theme.textTheme.bodyMedium),
            const Spacer(),
            const _HistorySparkline(series: HistorySeries.networkDown),
          ],
        );
      },
    );
  }
}

class DisksCard extends ConsumerWidget {
  const DisksCard({super.key});

  static double? _fullest(CardState card) =>
      disksFromJson(card.data)
          .map((d) => d.usedFraction * 100)
          .fold<double?>(null, (a, b) => a == null || b > a ? b : a);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.disks,
      title: l10n.cardDisks,
      icon: Icons.storage_rounded,
      readPermission: PermissionKeys.monitorView,
      severity: (card) => usageSeverity(_fullest(card), attention: 85, critical: 95),
      builder: (context, card) {
        final disks = disksFromJson(card.data);
        final theme = Theme.of(context);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (final disk in disks.take(2)) ...[
              Row(
                children: [
                  Text(disk.drive, style: theme.textTheme.titleSmall),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      l10n.diskFree(Units.bytes(disk.totalBytes - disk.usedBytes), Units.bytes(disk.totalBytes)),
                      style: theme.textTheme.bodySmall,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              LinearProgressIndicator(
                value: disk.usedFraction,
                color: usageSeverity(disk.usedFraction * 100, attention: 85, critical: 95).color,
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            if (disks.length > 2) Text('+${disks.length - 2}', style: theme.textTheme.bodySmall),
          ],
        );
      },
    );
  }
}

class BatteryCard extends ConsumerWidget {
  const BatteryCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.battery,
      title: l10n.cardBattery,
      icon: Icons.battery_std_rounded,
      readPermission: PermissionKeys.statusView,
      severity: (card) {
        final battery = BatteryData.fromJson(card.data);
        if (battery.charging) {
          return Severity.normal;
        }

        return battery.percent <= 10
            ? Severity.critical
            : battery.percent <= 20
            ? Severity.attention
            : Severity.normal;
      },
      builder: (context, card) {
        final battery = BatteryData.fromJson(card.data);
        final theme = Theme.of(context);
        final remaining = battery.secondsRemaining;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${battery.percent}%', style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700)),
            if (battery.charging)
              Text(l10n.batteryCharging, style: theme.textTheme.bodyMedium)
            else if (remaining != null)
              Text(
                l10n.batteryRemaining(formatDuration(l10n, Duration(seconds: remaining))),
                style: theme.textTheme.bodyMedium,
              ),
          ],
        );
      },
    );
  }
}

/// Card "PC e conexão": estado da conexão (spec 004) e o status do PC (spec 007).
class PcCard extends ConsumerWidget {
  const PcCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    DashboardDebug.record(DashboardCards.pc);
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final connection = ref.watch(agentConnectionControllerProvider);
    final card = ref.watch(dashboardCardProvider(DashboardCards.pc));
    final pc = card == null ? null : PcData.fromJson(card.data);

    final (severity, statusText) = switch (connection) {
      OnlineConnection() => (Severity.normal, l10n.connectionOnline),
      ReconnectingConnection(:final agentStopped) => (
        Severity.offline,
        agentStopped ? l10n.connectionAgentStopped : l10n.connectionReconnecting,
      ),
      ConnectingConnection() => (Severity.attention, l10n.connectionConnecting),
      _ => (Severity.offline, l10n.connectionOffline),
    };
    final name = switch (connection) {
      OnlineConnection(:final session) => pc?.hostname ?? session.pc.displayName,
      ConnectingConnection(:final pc) || ReconnectingConnection(:final pc) => pc.displayName,
      _ => '',
    };
    final address =
        pc?.addresses.firstOrNull?.address ?? (connection is OnlineConnection ? connection.session.host : null);

    return DashCard(
      key: const Key('card-pc'),
      title: l10n.cardPc,
      icon: Icons.computer_rounded,
      state: connection is ConnectingConnection && card == null ? DashCardState.loading : DashCardState.ready,
      trailing: connection is OnlineConnection ? const LiveIndicator() : null,
      onTap: card == null ? null : () => context.push('/card/${DashboardCards.pc}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          StatusBadge(severity: severity, label: statusText),
          const SizedBox(height: AppSpacing.sm),
          Text(name, style: theme.textTheme.titleMedium, overflow: TextOverflow.ellipsis),
          if (connection case ReconnectingConnection(:final offlineSince)) ...[
            const Spacer(),
            Row(
              children: [
                Expanded(child: _OfflineFor(since: offlineSince)),
                TextButton(
                  onPressed: ref.read(agentConnectionControllerProvider.notifier).retryNow,
                  child: Text(l10n.actionRetry),
                ),
              ],
            ),
          ] else ...[
            if (pc != null)
              Text('${pc.osName} · ${pc.osVersion}', style: theme.textTheme.bodySmall, overflow: TextOverflow.ellipsis),
            if (pc?.bootedAt case final bootedAt?) _Uptime(bootedAt: bootedAt),
            if (address != null) Text(address, style: theme.textTheme.bodySmall, overflow: TextOverflow.ellipsis),
          ],
        ],
      ),
    );
  }
}

/// Card "Energia" (spec 005): contagem ativa com "Cancelar", ou os botões das ações. Sem permissão a ação some;
/// sem suporte no PC ela aparece desabilitada.
class PowerCard extends ConsumerWidget {
  const PowerCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return DashboardCardFrame(
      cardId: DashboardCards.power,
      title: l10n.cardPower,
      icon: Icons.power_settings_new_rounded,
      readPermission: PermissionKeys.statusView,
      builder: (context, card) {
        final power = PowerData.fromJson(card.data);
        final pending = power.pending;
        return pending == null
            ? _PowerButtons(card: card)
            : _PowerCountdownView(
                countdown: pending,
                canCancel: card.availableActions.contains(PowerCardActions.cancel),
              );
      },
    );
  }
}

class _PowerButtons extends ConsumerWidget {
  const _PowerButtons({required this.card});

  final CardState card;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final permissions = ref.watch(currentPermissionsProvider);
    final actions = [
      for (final (action, icon) in const [
        (PowerActionKind.lock, Icons.lock_rounded),
        (PowerActionKind.sleep, Icons.bedtime_rounded),
        (PowerActionKind.restart, Icons.restart_alt_rounded),
        (PowerActionKind.hibernate, Icons.ac_unit_rounded),
        (PowerActionKind.shutdown, Icons.power_settings_new_rounded),
      ])
        if (permissions.contains(action.destructive ? PermissionKeys.powerShutdown : PermissionKeys.powerBasic))
          (action, icon),
    ];

    if (actions.isEmpty) {
      return Text(l10n.errorForbidden, style: Theme.of(context).textTheme.bodySmall);
    }

    return Row(
      children: [
        for (final (action, icon) in actions)
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                IconButton.filledTonal(
                  key: Key('power-${action.name}'),
                  tooltip: card.availableActions.contains(action.actionId)
                      ? powerActionLabel(l10n, action)
                      : '${powerActionLabel(l10n, action)} — ${l10n.powerUnavailable}',
                  onPressed: card.availableActions.contains(action.actionId)
                      ? () => runPowerAction(context, ref, action)
                      : null,
                  icon: Icon(icon),
                ),
                Text(
                  powerActionLabel(l10n, action),
                  style: Theme.of(context).textTheme.labelSmall,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Contagem ao vivo; vibra uma vez ao entrar nos últimos 10 s.
class _PowerCountdownView extends ConsumerStatefulWidget {
  const _PowerCountdownView({required this.countdown, required this.canCancel});

  final PowerCountdown countdown;
  final bool canCancel;

  @override
  ConsumerState<_PowerCountdownView> createState() => _PowerCountdownViewState();
}

class _PowerCountdownViewState extends ConsumerState<_PowerCountdownView> {
  static const _finalStretch = Duration(seconds: 10);

  late final Timer _ticker;
  bool _warned = false;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _ticker.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final countdown = widget.countdown;
    final remaining = countdown.executesAt.difference(ref.read(clockProvider)().toUtc());
    if (!_warned && remaining <= _finalStretch) {
      _warned = true;
      unawaited(HapticFeedback.heavyImpact());
    }

    final notes = [
      if (countdown.requestedByDeviceName case final name?) l10n.powerRequestedBy(name),
      if (countdown.force) l10n.powerForced,
      if (countdown.updatePending) l10n.powerUpdatePending,
    ];

    return Column(
      key: const Key('powerCountdown'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          powerPendingLabel(l10n, countdown.action, Units.countdown(remaining)),
          style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700, color: theme.colorScheme.error),
        ),
        Expanded(
          child: Text(notes.join('\n'), style: theme.textTheme.bodySmall, overflow: TextOverflow.fade),
        ),
        if (widget.canCancel)
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.tonal(
              key: const Key('powerCancel'),
              onPressed: () => cancelPowerAction(context, ref),
              child: Text(l10n.powerCancel),
            ),
          ),
      ],
    );
  }
}

/// Gauge + sparkline de 60 s + detalhe, para CPU, memória e GPU.
class _GaugeWithDetail extends StatelessWidget {
  const _GaugeWithDetail({
    required this.value,
    required this.label,
    required this.severity,
    required this.series,
    this.maxValue,
    this.detail,
  });

  final double? value;
  final String label;
  final Severity severity;
  final String series;
  final double? maxValue;
  final String? detail;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final value = this.value;
    return Row(
      children: [
        if (value == null)
          Tooltip(
            message: AppLocalizations.of(context).unavailableHint,
            child: Text(AppLocalizations.of(context).valueUnavailable, style: theme.textTheme.headlineSmall),
          )
        else
          Gauge(value: value / 100, label: label, severity: severity, size: 80),
        const SizedBox(width: AppSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _HistorySparkline(series: series, maxValue: maxValue),
              if (detail case final text? when text.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.xs),
                Text(text, style: theme.textTheme.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _HistorySparkline extends ConsumerWidget {
  const _HistorySparkline({required this.series, this.maxValue});

  final String series;
  final double? maxValue;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final values = ref.watch(dashboardHistoryProvider(series));
    return values.length < 2 ? const SizedBox(height: 36) : Sparkline(values: values, maxValue: maxValue);
  }
}

/// "Ligado há X", atualizado a cada minuto.
class _Uptime extends ConsumerStatefulWidget {
  const _Uptime({required this.bootedAt});

  final DateTime bootedAt;

  @override
  ConsumerState<_Uptime> createState() => _UptimeState();
}

class _UptimeState extends ConsumerState<_Uptime> {
  late final Timer _ticker;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(const Duration(minutes: 1), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _ticker.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final uptime = ref.read(clockProvider)().toUtc().difference(widget.bootedAt);
    return Text(l10n.pcUptime(formatDuration(l10n, uptime)), style: Theme.of(context).textTheme.bodySmall);
  }
}

/// "Offline há X", atualizado a cada segundo (spec 004 e AC-4 da spec 007).
class _OfflineFor extends ConsumerStatefulWidget {
  const _OfflineFor({required this.since});

  final DateTime since;

  @override
  ConsumerState<_OfflineFor> createState() => _OfflineForState();
}

class _OfflineForState extends ConsumerState<_OfflineFor> {
  late final Timer _ticker;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _ticker.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final age = ref.read(clockProvider)().difference(widget.since);
    return Text(
      l10n.connectionOfflineSince(formatDuration(l10n, age)),
      key: const Key('offlineSince'),
      style: Theme.of(context).textTheme.bodySmall,
    );
  }
}

double? _percent(double? fraction) => fraction == null ? null : fraction * 100;
