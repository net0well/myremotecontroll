import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import '../../features/settings/logic/app_settings.dart';
import '../../l10n/app_localizations.dart';
import '../widgets/slide_to_confirm.dart';
import 'app_lock.dart';

/// Confirmação de ação destrutiva (spec 004): diálogo com "deslize para confirmar" e, se a biometria estiver ligada,
/// biometria/PIN do aparelho. Devolve verdadeiro só se o usuário confirmou as duas etapas.
Future<bool> confirmDestructive(
  BuildContext context,
  WidgetRef ref, {
  required String title,
  required String message,
  required String slideLabel,
}) async {
  final reason = AppLocalizations.of(context).confirmReason;
  final slid = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (context) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(title, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: AppSpacing.md),
            Text(message),
            const SizedBox(height: AppSpacing.xl),
            SlideToConfirm(
              label: slideLabel,
              color: Theme.of(context).colorScheme.error,
              onConfirmed: () => Navigator.of(context).pop(true),
            ),
            const SizedBox(height: AppSpacing.sm),
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: Text(AppLocalizations.of(context).actionCancel),
            ),
          ],
        ),
      ),
    ),
  );

  if (slid != true || !context.mounted) {
    return false;
  }

  return (await confirmWithDevice(ref, reason)).confirmed;
}

/// Resultado da confirmação no aparelho: [authenticated] diz se houve biometria/PIN de fato (vai para a auditoria).
typedef DeviceConfirmation = ({bool confirmed, bool authenticated});

/// Biometria/PIN do aparelho, se o bloqueio estiver ligado nas configurações (spec 004). Sem bloqueio ligado ou sem
/// biometria/PIN configurados no aparelho, a confirmação passa sem autenticar.
Future<DeviceConfirmation> confirmWithDevice(WidgetRef ref, String reason) async {
  if (!ref.read(appSettingsControllerProvider).biometricLock) {
    return (confirmed: true, authenticated: false);
  }

  final authenticator = ref.read(deviceAuthenticatorProvider);
  if (!await authenticator.isAvailable()) {
    return (confirmed: true, authenticated: false);
  }

  final ok = await authenticator.authenticate(reason);
  return (confirmed: ok, authenticated: ok);
}
