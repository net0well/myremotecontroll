import 'dart:async';

import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/network/agent_api.dart';
import '../../../core/network/pin_registry.dart';
import '../../../core/storage/paired_pc.dart';
import 'pairing_link.dart';

/// Resultado do pareamento: o PC a salvar e o token, que só existe nesta resposta.
class PairingOutcome {
  const PairingOutcome(this.pc, this.token);

  final PairedPc pc;
  final String token;
}

/// Pareamento pelo link do QR (spec 002): fixa o pin, acha um endereço que responda e troca o código pelo token.
abstract interface class PairingService {
  Future<PairingOutcome> pair(PairingLink link, {required String deviceName, required DevicePlatform platform});
}

class NetworkPairingService implements PairingService {
  NetworkPairingService(this._pins);

  final PinRegistry _pins;

  @override
  Future<PairingOutcome> pair(PairingLink link, {required String deviceName, required DevicePlatform platform}) async {
    _pins.register(link.hosts, link.port, link.pin);

    // Só uma chamada de pareamento: o código é de uso único. Primeiro descobre qual endereço responde.
    final host = await _firstReachable(link);
    final api = AgentApi(host: host, port: link.port, pins: _pins);
    try {
      final json = await api.completePairing(code: link.code, deviceName: deviceName, platform: platform);
      final pcName = json['pcName'] as String? ?? link.pcName;
      return PairingOutcome(
        PairedPc(
          id: link.pin,
          name: pcName.isEmpty ? link.pcName : pcName,
          hosts: link.hosts,
          port: link.port,
          pin: link.pin,
          deviceId: json['deviceId'] as String,
          role: deviceRoleFromJson(json['role']),
          lastHost: host,
        ),
        json['token'] as String,
      );
    } finally {
      api.close();
    }
  }

  Future<String> _firstReachable(PairingLink link) {
    final completer = Completer<String>();
    final failures = <AgentException>[];
    var pending = link.hosts.length;
    for (final host in link.hosts) {
      final api = AgentApi(host: host, port: link.port, pins: _pins, connectTimeout: const Duration(seconds: 4));
      api
          .getInfo()
          .then(
            (_) {
              if (!completer.isCompleted) {
                completer.complete(host);
              }
            },
            onError: (Object error) {
              failures.add(error is AgentException ? error : const AgentException(AgentFailureKind.unreachable));
              if (--pending == 0 && !completer.isCompleted) {
                completer.completeError(
                  failures.where((f) => f.kind == AgentFailureKind.certificateMismatch).firstOrNull ?? failures.first,
                );
              }
            },
          )
          .whenComplete(api.close);
    }

    return completer.future;
  }
}
