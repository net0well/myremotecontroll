import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/storage/paired_pc.dart';
import '../../../core/storage/paired_pcs.dart';
import '../../../core/widgets/dash_card.dart';
import '../../../l10n/app_localizations.dart';
import '../../power/data/power_repository.dart';
import '../../power/ui/power_flow.dart';
import '../data/dashboard_models.dart';
import '../logic/dashboard_controller.dart';
import 'dashboard_cards.dart';

/// Painel (tela inicial, spec 007): cards ao vivo na ordem PC e conexão, Energia, CPU, Memória, Rede, Discos, GPU e
/// Bateria. Em segundo plano a assinatura é cancelada; ao voltar, chega um snapshot novo.
class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({super.key});

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  late final AppLifecycleListener _lifecycle;

  @override
  void initState() {
    super.initState();
    _lifecycle = AppLifecycleListener(
      onHide: () => unawaited(ref.read(dashboardControllerProvider.notifier).pause()),
      onShow: () => ref.read(dashboardControllerProvider.notifier).resume(),
    );
  }

  @override
  void dispose() {
    _lifecycle.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final connection = ref.watch(agentConnectionControllerProvider);

    // Contagem que terminou sem executar: avisa o motivo (cancelada no PC, falha do Windows…).
    ref.listen(powerCancellationsProvider, (_, next) {
      if (next case AsyncData(value: final cancelled) when cancelled.source != PowerCancelSource.device) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(powerCancelledMessage(l10n, cancelled.source))));
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const _PcSwitcherTitle(),
        actions: [
          IconButton(
            tooltip: l10n.settingsTitle,
            icon: const Icon(Icons.settings_rounded),
            onPressed: () => context.push('/settings'),
          ),
        ],
      ),
      body: SafeArea(
        child: switch (connection) {
          RevokedConnection(:final pc) => _BlockedState(
            icon: Icons.phonelink_erase_rounded,
            title: l10n.connectionRevoked,
            message: l10n.connectionRevokedHint,
            pc: pc,
          ),
          CertificateMismatchConnection(:final pc) => _BlockedState(
            icon: Icons.gpp_bad_rounded,
            title: l10n.connectionCertificateMismatch,
            message: l10n.connectionCertificateMismatchHint,
            pc: pc,
            danger: true,
          ),
          _ => const DashboardGrid(),
        },
      ),
    );
  }
}

/// Grade responsiva: as colunas vêm da largura disponível (1–2 no celular, 3–4 no tablet), nunca da orientação.
/// Só reconstrói quando um card aparece ou some (bateria, GPU); o conteúdo é de cada card.
class DashboardGrid extends ConsumerWidget {
  const DashboardGrid({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final hasGpu = ref.watch(dashboardControllerProvider.select((s) => s.cards.containsKey(DashboardCards.gpu)));
    final hasBattery = ref.watch(
      dashboardControllerProvider.select((s) => s.cards.containsKey(DashboardCards.battery)),
    );
    final cards = <Widget>[
      const PcCard(),
      const PowerCard(),
      const CpuCard(),
      const MemoryCard(),
      const NetworkCard(),
      const DisksCard(),
      if (hasGpu) const GpuCard(),
      if (hasBattery) const BatteryCard(),
      DashCard(
        title: l10n.cardClaude,
        icon: Icons.smart_toy_rounded,
        child: Text(l10n.comingSoonHint, style: Theme.of(context).textTheme.bodySmall),
      ),
    ];

    return LayoutBuilder(
      builder: (context, constraints) => GridView.builder(
        key: const Key('dashboardGrid'),
        padding: const EdgeInsets.all(AppSpacing.lg),
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: AppBreakpoints.columnsFor(constraints.maxWidth),
          mainAxisExtent: 184,
          crossAxisSpacing: AppSpacing.md,
          mainAxisSpacing: AppSpacing.md,
        ),
        itemCount: cards.length,
        itemBuilder: (context, index) => cards[index],
      ),
    );
  }
}

class _BlockedState extends StatelessWidget {
  const _BlockedState({
    required this.icon,
    required this.title,
    required this.message,
    required this.pc,
    this.danger = false,
  });

  final IconData icon;
  final String title;
  final String message;
  final PairedPc pc;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = danger ? theme.colorScheme.error : theme.colorScheme.primary;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 64, color: color),
              const SizedBox(height: AppSpacing.lg),
              Text(title, style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.sm),
              Text(pc.displayName, style: theme.textTheme.titleMedium),
              const SizedBox(height: AppSpacing.md),
              Text(message, style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.xl),
              FilledButton.icon(
                onPressed: () => context.push('/pairing'),
                icon: const Icon(Icons.qr_code_scanner_rounded),
                label: Text(AppLocalizations.of(context).connectionRepair),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PcSwitcherTitle extends ConsumerWidget {
  const _PcSwitcherTitle();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final pcs = ref.watch(pairedPcsProvider);
    return InkWell(
      borderRadius: BorderRadius.circular(AppRadii.chip),
      onTap: () => showModalBottomSheet<void>(context: context, builder: (_) => const PcSwitcherSheet()),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs, horizontal: AppSpacing.sm),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Flexible(child: Text(pcs.active?.displayName ?? l10n.dashboardTitle, overflow: TextOverflow.ellipsis)),
            const Icon(Icons.expand_more_rounded),
          ],
        ),
      ),
    );
  }
}

/// Troca rápida de PC (spec 004, AC-1).
class PcSwitcherSheet extends ConsumerWidget {
  const PcSwitcherSheet({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final state = ref.watch(pairedPcsProvider);
    return SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          ListTile(title: Text(l10n.pcSwitcherTitle, style: Theme.of(context).textTheme.titleMedium)),
          for (final pc in state.pcs)
            ListTile(
              key: Key('pc-${pc.id}'),
              leading: Icon(pc.id == state.activeId ? Icons.radio_button_checked : Icons.radio_button_unchecked),
              title: Text(pc.displayName),
              subtitle: Text(pc.hosts.join(', '), maxLines: 1, overflow: TextOverflow.ellipsis),
              onTap: () {
                ref.read(pairedPcsProvider.notifier).setActive(pc.id);
                Navigator.of(context).pop();
              },
            ),
          ListTile(
            leading: const Icon(Icons.add_rounded),
            title: Text(l10n.pcAdd),
            onTap: () {
              Navigator.of(context).pop();
              context.push('/pairing');
            },
          ),
        ],
      ),
    );
  }
}
