import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/contracts/contracts.dart';
import '../../../core/security/confirm_destructive.dart';
import '../../../core/widgets/slide_to_confirm.dart';
import '../../../l10n/app_localizations.dart';
import '../../dashboard/data/dashboard_models.dart';
import '../data/power_repository.dart';

/// Maior atraso aceito pelo agente: 24 h.
const maxPowerDelayMinutes = 24 * 60;

/// Fluxo de uma ação de energia (spec 005): folha de confirmação → pedido → tratamento das respostas.
/// Já pendente → "Substituir" (AC-8); trabalho ativo → confirmação extra (AC-3); indisponível → mensagem clara.
Future<void> runPowerAction(BuildContext context, WidgetRef ref, PowerActionKind action) async {
  var current = action;
  while (true) {
    if (!context.mounted) {
      return;
    }

    final choice = await showModalBottomSheet<PowerSheetResult>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => PowerActionSheet(action: current),
    );
    if (choice is PowerSwitchAction) {
      // "Reiniciar/Bloquear em vez disso" na folha de Desligar.
      current = choice.action;
      continue;
    }

    if (choice is! PowerConfirmed || !context.mounted) {
      return;
    }

    var request = choice.request;
    if (current.destructive) {
      final confirmation = await confirmWithDevice(ref, AppLocalizations.of(context).powerReason);
      if (!confirmation.confirmed || !context.mounted) {
        return;
      }

      request = PowerRequest(
        action: request.action,
        delaySeconds: request.delaySeconds,
        force: request.force,
        localConfirmation: confirmation.authenticated,
      );
    }

    await _send(context, ref, request);
    return;
  }
}

Future<void> _send(BuildContext context, WidgetRef ref, PowerRequest request, {bool replaced = false}) async {
  final l10n = AppLocalizations.of(context);
  final repository = ref.read(powerRepositoryProvider);
  if (repository == null) {
    _snack(context, l10n.errorNetwork);
    return;
  }

  final result = await repository.request(request);
  if (!context.mounted) {
    return;
  }

  if (result.ok) {
    await HapticFeedback.mediumImpact();
    return;
  }

  switch (result.errorCode) {
    case ErrorCodes.powerAlreadyScheduled when !replaced:
      if (await _confirm(
            context,
            l10n.powerAlreadyScheduledTitle,
            Text(l10n.powerAlreadyScheduledMessage),
            l10n.powerReplace,
          ) &&
          context.mounted) {
        await repository.cancel();
        if (context.mounted) {
          await _send(context, ref, request, replaced: true);
        }
      }
    case ErrorCodes.powerActiveWork when !request.acknowledgeActiveWork:
      final items = ActiveWorkItem.fromDetails(result.errorDetails);
      final content = Column(
        key: const Key('activeWorkList'),
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(l10n.powerActiveWorkMessage),
          const SizedBox(height: AppSpacing.sm),
          for (final item in items) Text('• ${item.description}'),
        ],
      );
      if (await _confirm(context, l10n.powerActiveWorkTitle, content, l10n.powerContinueAnyway) && context.mounted) {
        await _send(context, ref, request.acknowledged(), replaced: replaced);
      }
    case ErrorCodes.powerSleepUnavailable:
      _snack(context, l10n.powerSleepUnavailable);
    case ErrorCodes.powerHibernateUnavailable:
      _snack(context, l10n.powerHibernateUnavailable);
    case ErrorCodes.authForbidden:
      _snack(context, l10n.errorForbidden);
    default:
      _snack(context, result.errorMessage?.isNotEmpty == true ? result.errorMessage! : l10n.errorUnknown);
  }
}

Future<bool> _confirm(BuildContext context, String title, Widget content, String confirmLabel) async =>
    await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: content,
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(AppLocalizations.of(context).actionCancel),
          ),
          FilledButton(
            key: const Key('powerDialogConfirm'),
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(confirmLabel),
          ),
        ],
      ),
    ) ??
    false;

void _snack(BuildContext context, String message) =>
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(message)));

/// Cancela a contagem ativa (card de energia).
Future<void> cancelPowerAction(BuildContext context, WidgetRef ref) async {
  final l10n = AppLocalizations.of(context);
  final result = await ref.read(powerRepositoryProvider)?.cancel();
  if (!context.mounted) {
    return;
  }

  if (result == null) {
    _snack(context, l10n.errorNetwork);
  } else if (result.ok) {
    await HapticFeedback.lightImpact();
  } else if (result.errorCode == ErrorCodes.powerNothingToCancel) {
    _snack(context, l10n.powerNothingToCancel);
  } else {
    _snack(context, l10n.errorUnknown);
  }
}

sealed class PowerSheetResult {
  const PowerSheetResult();
}

final class PowerConfirmed extends PowerSheetResult {
  const PowerConfirmed(this.request);

  final PowerRequest request;
}

final class PowerSwitchAction extends PowerSheetResult {
  const PowerSwitchAction(this.action);

  final PowerActionKind action;
}

/// Folha de confirmação: quando (agora, 30 s, 1 min, 5 min, personalizado até 24 h), "forçar fechamento" (desligar e
/// reiniciar) e o aviso de Desligar (AC-5). Desligar, reiniciar e hibernar pedem deslizar; bloquear e suspender, um
/// botão.
class PowerActionSheet extends StatefulWidget {
  const PowerActionSheet({super.key, required this.action});

  final PowerActionKind action;

  @override
  State<PowerActionSheet> createState() => _PowerActionSheetState();
}

class _PowerActionSheetState extends State<PowerActionSheet> {
  static const _presets = [0, 30, 60, 300];

  int _delaySeconds = 0;
  bool _force = false;

  PowerRequest get _request =>
      PowerRequest(action: widget.action, delaySeconds: _delaySeconds, force: widget.action.closesApps && _force);

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final action = widget.action;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          AppSpacing.xl,
          0,
          AppSpacing.xl,
          AppSpacing.xl + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(powerSheetTitle(l10n, action), style: theme.textTheme.headlineSmall),
            if (action == PowerActionKind.shutdown) ...[
              const SizedBox(height: AppSpacing.md),
              _ShutdownWarning(onSwitch: (other) => Navigator.of(context).pop(PowerSwitchAction(other))),
            ],
            const SizedBox(height: AppSpacing.lg),
            Text(l10n.powerDelay, style: theme.textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: [
                for (final seconds in _presets)
                  ChoiceChip(
                    key: Key('delay-$seconds'),
                    label: Text(_delayLabel(l10n, seconds)),
                    selected: _delaySeconds == seconds,
                    onSelected: (_) => setState(() => _delaySeconds = seconds),
                  ),
                ChoiceChip(
                  key: const Key('delay-custom'),
                  label: Text(
                    _presets.contains(_delaySeconds) ? l10n.powerDelayCustom : _delayLabel(l10n, _delaySeconds),
                  ),
                  selected: !_presets.contains(_delaySeconds),
                  onSelected: (_) => _pickCustomDelay(),
                ),
              ],
            ),
            if (action.closesApps) ...[
              const SizedBox(height: AppSpacing.md),
              SwitchListTile(
                key: const Key('powerForce'),
                contentPadding: EdgeInsets.zero,
                title: Text(l10n.powerForce),
                subtitle: Text(l10n.powerForceHint),
                value: _force,
                onChanged: (value) => setState(() => _force = value),
              ),
            ],
            const SizedBox(height: AppSpacing.xl),
            if (action.destructive)
              SlideToConfirm(
                key: const Key('powerSlide'),
                label: l10n.powerSlide,
                color: theme.colorScheme.error,
                onConfirmed: () => Navigator.of(context).pop(PowerConfirmed(_request)),
              )
            else
              FilledButton(
                key: const Key('powerConfirm'),
                onPressed: () => Navigator.of(context).pop(PowerConfirmed(_request)),
                child: Text(powerActionLabel(l10n, action)),
              ),
            const SizedBox(height: AppSpacing.sm),
            TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(l10n.actionCancel)),
          ],
        ),
      ),
    );
  }

  String _delayLabel(AppLocalizations l10n, int seconds) => switch (seconds) {
    0 => l10n.powerDelayNow,
    < 60 => l10n.durationSeconds(seconds),
    < 3600 => l10n.durationMinutes(seconds ~/ 60),
    _ => l10n.powerDelayHoursMinutes(seconds ~/ 3600, (seconds % 3600) ~/ 60),
  };

  Future<void> _pickCustomDelay() async {
    final minutes = await showDialog<int>(context: context, builder: (_) => const _CustomDelayDialog());
    if (minutes != null && mounted) {
      setState(() => _delaySeconds = minutes * 60);
    }
  }
}

/// Aviso de Desligar (AC-5): o PC só liga presencialmente; sugere Reiniciar ou Bloquear.
class _ShutdownWarning extends StatelessWidget {
  const _ShutdownWarning({required this.onSwitch});

  final ValueChanged<PowerActionKind> onSwitch;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Container(
      key: const Key('shutdownWarning'),
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(color: scheme.errorContainer, borderRadius: BorderRadius.circular(AppRadii.chip)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(Icons.warning_amber_rounded, color: scheme.onErrorContainer),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(l10n.powerShutdownWarning, style: TextStyle(color: scheme.onErrorContainer)),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            spacing: AppSpacing.sm,
            children: [
              OutlinedButton(
                key: const Key('useRestart'),
                onPressed: () => onSwitch(PowerActionKind.restart),
                child: Text(l10n.powerUseRestart),
              ),
              OutlinedButton(
                key: const Key('useLock'),
                onPressed: () => onSwitch(PowerActionKind.lock),
                child: Text(l10n.powerUseLock),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _CustomDelayDialog extends StatefulWidget {
  const _CustomDelayDialog();

  @override
  State<_CustomDelayDialog> createState() => _CustomDelayDialogState();
}

class _CustomDelayDialogState extends State<_CustomDelayDialog> {
  final _controller = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return AlertDialog(
      title: Text(l10n.powerDelayCustomTitle),
      content: TextField(
        key: const Key('customDelayMinutes'),
        controller: _controller,
        autofocus: true,
        keyboardType: TextInputType.number,
        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
        decoration: InputDecoration(labelText: l10n.powerDelayCustomHint, errorText: _error),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(l10n.actionCancel)),
        FilledButton(
          onPressed: () {
            final minutes = int.tryParse(_controller.text);
            if (minutes == null || minutes < 1 || minutes > maxPowerDelayMinutes) {
              setState(() => _error = l10n.powerDelayCustomInvalid);
              return;
            }

            Navigator.of(context).pop(minutes);
          },
          child: Text(l10n.actionConfirm),
        ),
      ],
    );
  }
}

String powerActionLabel(AppLocalizations l10n, PowerActionKind action) => switch (action) {
  PowerActionKind.lock => l10n.powerLock,
  PowerActionKind.sleep => l10n.powerSleep,
  PowerActionKind.restart => l10n.powerRestart,
  PowerActionKind.hibernate => l10n.powerHibernate,
  PowerActionKind.shutdown => l10n.powerShutdown,
};

String powerSheetTitle(AppLocalizations l10n, PowerActionKind action) => switch (action) {
  PowerActionKind.lock => l10n.powerTitleLock,
  PowerActionKind.sleep => l10n.powerTitleSleep,
  PowerActionKind.restart => l10n.powerTitleRestart,
  PowerActionKind.hibernate => l10n.powerTitleHibernate,
  PowerActionKind.shutdown => l10n.powerTitleShutdown,
};

String powerPendingLabel(AppLocalizations l10n, PowerActionKind action, String time) => switch (action) {
  PowerActionKind.lock => l10n.powerPendingLock(time),
  PowerActionKind.sleep => l10n.powerPendingSleep(time),
  PowerActionKind.restart => l10n.powerPendingRestart(time),
  PowerActionKind.hibernate => l10n.powerPendingHibernate(time),
  PowerActionKind.shutdown => l10n.powerPendingShutdown(time),
};

String powerCancelledMessage(AppLocalizations l10n, PowerCancelSource source) => switch (source) {
  PowerCancelSource.device => l10n.powerCancelledDevice,
  PowerCancelSource.tray => l10n.powerCancelledTray,
  PowerCancelSource.agentShutdown => l10n.powerCancelledAgent,
  PowerCancelSource.failed => l10n.powerCancelledFailed,
};
