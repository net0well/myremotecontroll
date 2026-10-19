import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/formatting/durations.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/providers.dart';
import '../../../core/security/confirm_destructive.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../l10n/app_localizations.dart';
import '../data/security_repository.dart';
import '../logic/security_controllers.dart';

String roleLabel(AppLocalizations l10n, DeviceRole role) => switch (role) {
  DeviceRole.readOnly => l10n.roleReadOnly,
  DeviceRole.operator => l10n.roleOperator,
  DeviceRole.administrator => l10n.roleAdministrator,
};

/// Segurança → Aparelhos (spec 003): papéis e revogação. Só aparece para quem tem `security.manageDevices`.
class DevicesScreen extends ConsumerWidget {
  const DevicesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final devices = ref.watch(devicesControllerProvider);
    final connection = ref.watch(agentConnectionControllerProvider);
    final myDeviceId = connection is OnlineConnection ? connection.session.info.deviceId : null;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.securityDevices)),
      body: devices.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(error: error, onRetry: () => ref.invalidate(devicesControllerProvider)),
        data: (items) => ListView.separated(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
          itemCount: items.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) => _DeviceTile(device: items[index], isMe: items[index].id == myDeviceId),
        ),
      ),
    );
  }
}

class _DeviceTile extends ConsumerWidget {
  const _DeviceTile({required this.device, required this.isMe});

  final DeviceInfo device;
  final bool isMe;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final now = ref.read(clockProvider)();
    final subtitle = device.revoked
        ? l10n.devicesRevoked
        : device.connected
        ? l10n.devicesConnected
        : device.lastSeenAt == null
        ? l10n.devicesNeverSeen
        : l10n.devicesLastSeen(formatDuration(l10n, now.difference(device.lastSeenAt!)));

    return ListTile(
      leading: Icon(device.platform == DevicePlatform.ios ? Icons.phone_iphone_rounded : Icons.phone_android_rounded),
      title: Text(isMe ? '${device.name} · ${l10n.devicesThisDevice}' : device.name),
      subtitle: Text('${roleLabel(l10n, device.role)} · $subtitle'),
      enabled: !device.revoked,
      trailing: device.revoked
          ? null
          : PopupMenuButton<Object>(
              onSelected: (choice) => _onSelected(context, ref, choice),
              itemBuilder: (context) => [
                for (final role in DeviceRole.values)
                  CheckedPopupMenuItem(value: role, checked: role == device.role, child: Text(roleLabel(l10n, role))),
                const PopupMenuDivider(),
                PopupMenuItem(value: 'revoke', child: Text(l10n.devicesRevoke)),
              ],
            ),
    );
  }

  Future<void> _onSelected(BuildContext context, WidgetRef ref, Object choice) async {
    final l10n = AppLocalizations.of(context);
    final controller = ref.read(devicesControllerProvider.notifier);
    try {
      if (choice is DeviceRole && choice != device.role) {
        await controller.changeRole(device, choice);
      } else if (choice == 'revoke' &&
          await confirmDestructive(
            context,
            ref,
            title: l10n.devicesRevoke,
            message: l10n.devicesRevokeConfirm(device.name),
            slideLabel: l10n.confirmSlide,
          )) {
        await controller.revoke(device);
      }
    } on AgentException catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message(l10n))));
      }
    }
  }
}

/// Segurança → Auditoria (spec 003). Só aparece para quem tem `security.viewAudit`.
class AuditScreen extends ConsumerWidget {
  const AuditScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final audit = ref.watch(auditControllerProvider);
    final format = DateFormat.yMd('pt').add_Hm();

    return Scaffold(
      appBar: AppBar(title: Text(l10n.securityAudit)),
      body: audit.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(error: error, onRetry: () => ref.invalidate(auditControllerProvider)),
        data: (state) => state.items.isEmpty
            ? Center(child: Text(l10n.auditEmpty))
            : ListView.builder(
                itemCount: state.items.length + (state.nextCursor == null ? 0 : 1),
                itemBuilder: (context, index) {
                  if (index == state.items.length) {
                    return Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: OutlinedButton(
                        onPressed: state.loadingMore ? null : ref.read(auditControllerProvider.notifier).loadMore,
                        child: Text(l10n.auditLoadMore),
                      ),
                    );
                  }

                  final item = state.items[index];
                  final (severity, label) = switch (item.result) {
                    'Success' => (Severity.normal, l10n.auditResultSuccess),
                    'Denied' => (Severity.critical, l10n.auditResultDenied),
                    _ => (Severity.attention, l10n.auditResultFailed),
                  };
                  return ListTile(
                    title: Text(item.action),
                    subtitle: Text(
                      [format.format(item.timestamp.toLocal()), ?item.deviceName, ?item.errorCode].join(' · '),
                    ),
                    trailing: StatusBadge(severity: severity, label: label),
                  );
                },
              ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final message = error is AgentException ? (error as AgentException).message(l10n) : l10n.errorUnknown;
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: AppSpacing.md),
          OutlinedButton(onPressed: onRetry, child: Text(l10n.actionRetry)),
        ],
      ),
    );
  }
}
