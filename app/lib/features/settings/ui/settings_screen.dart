import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/contracts/contracts.dart';
import '../../../core/storage/paired_pc.dart';
import '../../../core/storage/paired_pcs.dart';
import '../../../l10n/app_localizations.dart';
import '../../security/logic/security_controllers.dart';
import '../logic/app_settings.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final settings = ref.watch(appSettingsControllerProvider);
    final controller = ref.read(appSettingsControllerProvider.notifier);
    final pcs = ref.watch(pairedPcsProvider);
    final permissions = ref.watch(currentPermissionsProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.settingsTitle)),
      body: ListView(
        children: [
          _Section(l10n.settingsPcs),
          for (final pc in pcs.pcs) _PcTile(pc: pc, active: pc.id == pcs.activeId),
          ListTile(
            leading: const Icon(Icons.add_rounded),
            title: Text(l10n.pcAdd),
            onTap: () => context.push('/pairing'),
          ),
          _Section(l10n.settingsSecurity),
          SwitchListTile(
            title: Text(l10n.settingsBiometricLock),
            value: settings.biometricLock,
            onChanged: (value) => controller.change((s) => s.copyWith(biometricLock: value)),
          ),
          ListTile(
            title: Text(l10n.settingsLockTimeout(settings.lockTimeoutMinutes)),
            enabled: settings.biometricLock,
            trailing: DropdownButton<int>(
              value: settings.lockTimeoutMinutes,
              onChanged: settings.biometricLock
                  ? (value) => controller.change((s) => s.copyWith(lockTimeoutMinutes: value))
                  : null,
              items: [
                for (final minutes in AppSettings.lockTimeoutOptions)
                  DropdownMenuItem(value: minutes, child: Text(l10n.durationMinutes(minutes))),
              ],
            ),
          ),
          if (permissions.contains(PermissionKeys.securityManageDevices))
            ListTile(
              key: const Key('settings-devices'),
              leading: const Icon(Icons.devices_rounded),
              title: Text(l10n.securityDevices),
              onTap: () => context.push('/settings/devices'),
            ),
          if (permissions.contains(PermissionKeys.securityViewAudit))
            ListTile(
              key: const Key('settings-audit'),
              leading: const Icon(Icons.receipt_long_rounded),
              title: Text(l10n.securityAudit),
              onTap: () => context.push('/settings/audit'),
            ),
          _Section(l10n.settingsAppearance),
          RadioGroup<ThemeMode>(
            groupValue: settings.themeMode,
            onChanged: (mode) => controller.change((s) => s.copyWith(themeMode: mode)),
            child: Column(
              children: [
                RadioListTile(value: ThemeMode.system, title: Text(l10n.themeSystem)),
                RadioListTile(value: ThemeMode.dark, title: Text(l10n.themeDark)),
                RadioListTile(value: ThemeMode.light, title: Text(l10n.themeLight)),
              ],
            ),
          ),
          if (Platform.isAndroid)
            SwitchListTile(
              title: Text(l10n.settingsDynamicColor),
              value: settings.dynamicColor,
              onChanged: (value) => controller.change((s) => s.copyWith(dynamicColor: value)),
            ),
          const SizedBox(height: AppSpacing.xl),
        ],
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section(this.title);

  final String title;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xl, AppSpacing.lg, AppSpacing.sm),
    child: Text(
      title,
      style: Theme.of(context).textTheme.titleSmall?.copyWith(color: Theme.of(context).colorScheme.primary),
    ),
  );
}

class _PcTile extends ConsumerWidget {
  const _PcTile({required this.pc, required this.active});

  final PairedPc pc;
  final bool active;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final pcs = ref.read(pairedPcsProvider.notifier);
    return ListTile(
      leading: Icon(active ? Icons.computer_rounded : Icons.computer_outlined),
      title: Text(pc.displayName),
      subtitle: Text(
        pc.alias?.isNotEmpty == true ? pc.name : pc.hosts.join(', '),
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      selected: active,
      onTap: () => pcs.setActive(pc.id),
      trailing: PopupMenuButton<String>(
        onSelected: (choice) async {
          if (choice == 'rename') {
            final alias = await _askAlias(context, pc);
            if (alias != null) {
              await pcs.update(pc.copyWith(alias: alias));
            }
          } else if (choice == 'remove' && await _confirmRemove(context, pc)) {
            await pcs.remove(pc.id);
          }
        },
        itemBuilder: (_) => [
          PopupMenuItem(value: 'rename', child: Text(l10n.actionRename)),
          PopupMenuItem(value: 'remove', child: Text(l10n.actionRemove)),
        ],
      ),
    );
  }

  Future<String?> _askAlias(BuildContext context, PairedPc pc) {
    final l10n = AppLocalizations.of(context);
    final controller = TextEditingController(text: pc.alias ?? pc.name);
    return showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.pcAlias),
        content: TextField(controller: controller, autofocus: true, maxLength: 60),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: Text(l10n.actionCancel)),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: Text(l10n.actionSave)),
        ],
      ),
    ).whenComplete(controller.dispose);
  }

  Future<bool> _confirmRemove(BuildContext context, PairedPc pc) async {
    final l10n = AppLocalizations.of(context);
    return await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            content: Text(l10n.pcRemoveConfirm(pc.displayName)),
            actions: [
              TextButton(onPressed: () => Navigator.pop(context, false), child: Text(l10n.actionCancel)),
              FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(l10n.actionRemove)),
            ],
          ),
        ) ??
        false;
  }
}
