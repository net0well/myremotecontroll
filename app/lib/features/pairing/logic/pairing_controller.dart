import 'dart:io';

import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/providers.dart';
import '../../../core/storage/paired_pcs.dart';
import '../data/pairing_link.dart';
import '../data/pairing_service.dart';

part 'pairing_controller.g.dart';

sealed class PairingState {
  const PairingState();
}

final class PairingIdle extends PairingState {
  const PairingIdle();
}

/// Pedido enviado; o agente segura a resposta até a confirmação no PC.
final class PairingInProgress extends PairingState {
  const PairingInProgress();
}

final class PairingSucceeded extends PairingState {
  const PairingSucceeded(this.pcName);

  final String pcName;
}

final class PairingFailed extends PairingState {
  const PairingFailed({this.error, this.invalidLink = false});

  final AgentException? error;
  final bool invalidLink;
}

@Riverpod(keepAlive: true)
PairingService pairingService(Ref ref) => NetworkPairingService(ref.watch(pinRegistryProvider));

@riverpod
class PairingController extends _$PairingController {
  @override
  PairingState build() => const PairingIdle();

  Future<void> pair(String rawLink, String deviceName) async {
    final link = PairingLink.tryParse(rawLink);
    if (link == null) {
      state = const PairingFailed(invalidLink: true);
      return;
    }

    state = const PairingInProgress();
    try {
      final outcome = await ref
          .read(pairingServiceProvider)
          .pair(
            link,
            deviceName: deviceName.trim(),
            platform: Platform.isIOS ? DevicePlatform.ios : DevicePlatform.android,
          );
      await ref.read(pairedPcsProvider.notifier).add(outcome.pc, outcome.token);
      state = PairingSucceeded(outcome.pc.name);
    } on AgentException catch (error) {
      state = PairingFailed(error: error);
    }
  }

  void reset() => state = const PairingIdle();
}
