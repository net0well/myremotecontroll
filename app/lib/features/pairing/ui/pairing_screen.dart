import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:material_ui/material_ui.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../app/theme/tokens.dart';
import '../../../l10n/app_localizations.dart';
import '../data/pairing_link.dart';
import '../logic/pairing_controller.dart';

part 'pairing_screen.g.dart';

/// Os testes de widget desligam a câmera (o scanner usa canais de plataforma).
@Riverpod(keepAlive: true)
bool qrScannerEnabled(Ref ref) => true;

class PairingScreen extends ConsumerStatefulWidget {
  const PairingScreen({super.key});

  @override
  ConsumerState<PairingScreen> createState() => _PairingScreenState();
}

class _PairingScreenState extends ConsumerState<PairingScreen> {
  final _link = TextEditingController();
  late final _deviceName = TextEditingController(text: AppLocalizations.of(context).pairingDeviceNameDefault);

  @override
  void dispose() {
    _link.dispose();
    _deviceName.dispose();
    super.dispose();
  }

  void _start() => ref.read(pairingControllerProvider.notifier).pair(_link.text, _deviceName.text);

  void _onScanned(BarcodeCapture capture) {
    final value = capture.barcodes.map((b) => b.rawValue).whereType<String>().firstOrNull;
    if (value == null ||
        PairingLink.tryParse(value) == null ||
        ref.read(pairingControllerProvider) is PairingInProgress) {
      return;
    }

    HapticFeedback.selectionClick();
    _link.text = value;
    _start();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final state = ref.watch(pairingControllerProvider);
    final inProgress = state is PairingInProgress;

    ref.listen(pairingControllerProvider, (_, next) {
      if (next is PairingSucceeded) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(l10n.pairingSuccess(next.pcName))));
        context.go('/');
      }
    });

    return Scaffold(
      appBar: AppBar(title: Text(l10n.pairingTitle)),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                Text(l10n.pairingIntro, style: theme.textTheme.bodyLarge),
                const SizedBox(height: AppSpacing.lg),
                if (ref.watch(qrScannerEnabledProvider))
                  ClipRRect(
                    borderRadius: BorderRadius.circular(AppRadii.card),
                    child: SizedBox(
                      height: 280,
                      // O mobile_scanner ainda usa o Material do SDK; a ponte entrega tema e localização a ele.
                      // ignore: deprecated_member_use
                      child: MaterialUiCompatibilityBridge(
                        child: MobileScanner(
                          onDetect: _onScanned,
                          errorBuilder: (context, _) => Center(
                            child: Padding(
                              padding: const EdgeInsets.all(AppSpacing.lg),
                              child: Text(l10n.pairingCameraDenied, textAlign: TextAlign.center),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                const SizedBox(height: AppSpacing.lg),
                TextField(
                  key: const Key('pairing-link'),
                  controller: _link,
                  enabled: !inProgress,
                  decoration: InputDecoration(labelText: l10n.pairingManual, hintText: l10n.pairingManualHint),
                  maxLines: 2,
                  minLines: 1,
                ),
                const SizedBox(height: AppSpacing.md),
                TextField(
                  key: const Key('pairing-device-name'),
                  controller: _deviceName,
                  enabled: !inProgress,
                  maxLength: 100,
                  decoration: InputDecoration(labelText: l10n.pairingDeviceName),
                ),
                const SizedBox(height: AppSpacing.sm),
                if (inProgress)
                  Row(
                    children: [
                      const SizedBox.square(dimension: 24, child: CircularProgressIndicator(strokeWidth: 3)),
                      const SizedBox(width: AppSpacing.md),
                      Expanded(child: Text(l10n.pairingWaitingConfirmation, style: theme.textTheme.titleMedium)),
                    ],
                  )
                else
                  FilledButton.icon(
                    key: const Key('pairing-start'),
                    onPressed: _start,
                    icon: const Icon(Icons.link_rounded),
                    label: Text(l10n.pairingStart),
                  ),
                if (state is PairingFailed) ...[
                  const SizedBox(height: AppSpacing.md),
                  Text(
                    state.invalidLink ? l10n.pairingInvalidLink : state.error!.message(l10n),
                    style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
