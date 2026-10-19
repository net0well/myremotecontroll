import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/formatting/units.dart';
import '../../../l10n/app_localizations.dart';
import '../data/dashboard_models.dart';
import '../logic/dashboard_controller.dart';

/// Detalhe simples de um card (spec 007): tudo o que o card resume, ao vivo.
class CardDetailScreen extends ConsumerWidget {
  const CardDetailScreen({super.key, required this.cardId});

  final String cardId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final card = ref.watch(dashboardCardProvider(cardId));
    final rows = card == null ? const <(String, String)>[] : _rows(l10n, card);

    return Scaffold(
      appBar: AppBar(title: Text(_title(l10n, cardId))),
      body: rows.isEmpty
          ? Center(child: Text(l10n.valueUnavailable))
          : ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.lg),
              itemCount: rows.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) => ListTile(
                title: Text(rows[index].$1),
                trailing: Text(rows[index].$2, style: Theme.of(context).textTheme.titleMedium),
              ),
            ),
    );
  }

  static String _title(AppLocalizations l10n, String cardId) => switch (cardId) {
    DashboardCards.pc => l10n.cardPc,
    DashboardCards.power => l10n.cardPower,
    DashboardCards.cpu => l10n.cardCpu,
    DashboardCards.memory => l10n.cardMemory,
    DashboardCards.network => l10n.cardNetwork,
    DashboardCards.disks => l10n.cardDisks,
    DashboardCards.gpu => l10n.cardGpu,
    DashboardCards.battery => l10n.cardBattery,
    _ => cardId,
  };

  static List<(String, String)> _rows(AppLocalizations l10n, CardState card) => switch (card.cardId) {
    DashboardCards.cpu => () {
      final cpu = CpuData.fromJson(card.data);
      return [
        (l10n.cardCpu, Units.percent(cpu.usagePercent)),
        if (cpu.clockMhz != null) (l10n.cpuClock(''), Units.clock(cpu.clockMhz)),
        for (final (index, core) in cpu.cores.indexed) (l10n.cpuCore(index), Units.percent(core)),
      ];
    }(),
    DashboardCards.memory => () {
      final memory = MemoryData.fromJson(card.data);
      return [(l10n.cardMemory, l10n.memoryUsage(Units.bytes(memory.usedBytes), Units.bytes(memory.totalBytes)))];
    }(),
    DashboardCards.network => () {
      final network = NetworkData.fromJson(card.data);
      return [
        (l10n.cardNetwork, '${l10n.networkDown(Units.rate(network.down))}  ${l10n.networkUp(Units.rate(network.up))}'),
        for (final item in network.interfaces)
          (item.name, '${l10n.networkDown(Units.rate(item.down))}  ${l10n.networkUp(Units.rate(item.up))}'),
      ];
    }(),
    DashboardCards.disks => [
      for (final disk in disksFromJson(card.data)) ...[
        (
          [disk.drive, ?disk.label].join(' '),
          l10n.diskFree(Units.bytes(disk.totalBytes - disk.usedBytes), Units.bytes(disk.totalBytes)),
        ),
        ('', l10n.diskIo(Units.rate(disk.readBytesPerSecond), Units.rate(disk.writeBytesPerSecond))),
      ],
    ],
    DashboardCards.gpu => () {
      final gpu = GpuData.fromJson(card.data);
      return [
        if (gpu.name != null) (l10n.cardGpu, gpu.name!),
        (l10n.cardGpu, Units.percent(gpu.usagePercent)),
        if (gpu.dedicatedUsedBytes != null) (l10n.gpuVram(''), Units.bytes(gpu.dedicatedUsedBytes)),
      ];
    }(),
    DashboardCards.pc => () {
      final pc = PcData.fromJson(card.data);
      return [
        (pc.hostname, '${pc.osName} · ${pc.osVersion}'),
        (l10n.pcUser(''), pc.userName),
        if (pc.powerPlan != null) (l10n.pcPowerPlan(''), pc.powerPlan!),
        for (final address in pc.addresses)
          (
            address.interfaceName,
            address.kind == 'tailscale' ? '${address.address} (${l10n.networkTailscale})' : address.address,
          ),
      ];
    }(),
    DashboardCards.battery => () {
      final battery = BatteryData.fromJson(card.data);
      return [(l10n.cardBattery, '${battery.percent}%${battery.charging ? ' · ${l10n.batteryCharging}' : ''}')];
    }(),
    _ => const <(String, String)>[],
  };
}
