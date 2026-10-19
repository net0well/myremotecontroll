import 'dart:async';
import 'dart:math';

import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../contracts/contracts.dart';
import '../errors/agent_exception.dart';
import '../providers.dart';
import '../storage/paired_pc.dart';
import '../storage/paired_pcs.dart';
import 'agent_transport.dart';

part 'agent_connection.g.dart';

/// Estados da conexão com o PC ativo (spec 004).
sealed class AgentConnection {
  const AgentConnection();
}

final class NoPcConnection extends AgentConnection {
  const NoPcConnection();
}

final class ConnectingConnection extends AgentConnection {
  const ConnectingConnection(this.pc);

  final PairedPc pc;
}

final class OnlineConnection extends AgentConnection {
  const OnlineConnection(this.session, this.since);

  final AgentSession session;
  final DateTime since;
}

/// Sem conexão; o app segue tentando. [lastInfo] é o último estado conhecido, para mostrar enquanto isso.
/// [agentStopped]: a queda veio de um encerramento avisado pelo agente (`AgentShuttingDown`), não de falha de rede.
final class ReconnectingConnection extends AgentConnection {
  const ReconnectingConnection(
    this.pc, {
    required this.offlineSince,
    required this.attempt,
    this.lastInfo,
    this.agentStopped = false,
  });

  final PairedPc pc;
  final DateTime offlineSince;
  final int attempt;
  final AgentInfo? lastInfo;
  final bool agentStopped;
}

/// Token revogado ou desconhecido: só reparear resolve.
final class RevokedConnection extends AgentConnection {
  const RevokedConnection(this.pc);

  final PairedPc pc;
}

/// O certificado do PC não tem o pin pareado: conexão bloqueada, sem opção de prosseguir (spec 002, AC-3).
final class CertificateMismatchConnection extends AgentConnection {
  const CertificateMismatchConnection(this.pc);

  final PairedPc pc;
}

/// Intervalos entre tentativas de reconexão; os testes sobrescrevem com valores curtos.
@Riverpod(keepAlive: true)
List<Duration> reconnectDelays(Ref ref) => const [
  Duration(seconds: 1),
  Duration(seconds: 2),
  Duration(seconds: 4),
  Duration(seconds: 8),
  Duration(seconds: 16),
  Duration(seconds: 30),
];

@Riverpod(keepAlive: true)
AgentTransport agentTransport(Ref ref) => NetworkAgentTransport(ref.watch(pinRegistryProvider));

/// Mantém a conexão com o PC ativo: tenta todos os endereços em paralelo, reconecta com espera exponencial e na volta
/// da rede, e troca de PC na hora (spec 004, AC-1 e AC-2).
@Riverpod(keepAlive: true)
class AgentConnectionController extends _$AgentConnectionController {
  final Random _random = Random();
  int _generation = 0;
  Timer? _retry;
  AgentSession? _session;
  bool _agentStopping = false;

  @override
  AgentConnection build() {
    final pcId = ref.watch(activePcIdProvider);
    final generation = ++_generation;
    ref.onDispose(() => _teardown(generation));
    ref.listen(networkAvailableProvider, (_, _) => _retryNow());

    final pc = pcId == null ? null : ref.read(pairedPcsProvider).active;
    if (pc == null) {
      return const NoPcConnection();
    }

    scheduleMicrotask(() => _connect(pc, generation, attempt: 0, offlineSince: null));
    return ConnectingConnection(pc);
  }

  /// Força uma tentativa agora (ex.: "Tentar de novo" na tela).
  void retryNow() => _retryNow();

  void _retryNow() {
    final current = state;
    if (current is ReconnectingConnection) {
      _retry?.cancel();
      unawaited(_connect(current.pc, _generation, attempt: current.attempt, offlineSince: current.offlineSince));
    }
  }

  /// A tentativa ainda vale: o provider não foi descartado e ninguém trocou de PC nem reiniciou a conexão.
  bool _current(int generation) => ref.mounted && generation == _generation;

  Future<void> _connect(PairedPc pc, int generation, {required int attempt, required DateTime? offlineSince}) async {
    if (!_current(generation)) {
      return;
    }

    final pcs = ref.read(pairedPcsProvider.notifier);
    final token = await pcs.tokenOf(pc.id);
    if (!_current(generation)) {
      return;
    }

    if (token == null) {
      state = RevokedConnection(pc);
      return;
    }

    try {
      final (host, info) = await _probeAll(pc, token);
      if (!_current(generation)) {
        return;
      }

      final session = await ref
          .read(agentTransportProvider)
          .open(pc, host, token, info, onClosed: (_) => _onSessionClosed(generation));
      if (!_current(generation)) {
        await session.close();
        return;
      }

      _session = session;
      _agentStopping = false;
      _registerEvents(session, generation);
      state = OnlineConnection(session, ref.read(clockProvider)());
      unawaited(_rememberHost(pc, host, info));
      if (info.tokenRotationDue) {
        unawaited(_rotateToken(pc, host, token));
      }
    } on AgentException catch (error) {
      if (!_current(generation)) {
        return;
      }

      switch (error.kind) {
        case AgentFailureKind.unauthorized:
          state = RevokedConnection(pc);
        case AgentFailureKind.certificateMismatch:
          state = CertificateMismatchConnection(pc);
        case AgentFailureKind.unreachable || AgentFailureKind.rejected:
          _scheduleRetry(pc, generation, attempt, offlineSince ?? ref.read(clockProvider)());
      }
    }
  }

  /// Tenta todos os endereços ao mesmo tempo; vale o primeiro que responder. Sem nenhum, devolve a falha mais
  /// significativa (certificado divergente > token inválido > PC inalcançável).
  Future<(String, AgentInfo)> _probeAll(PairedPc pc, String token) {
    final transport = ref.read(agentTransportProvider);
    final completer = Completer<(String, AgentInfo)>();
    final failures = <AgentException>[];
    final hosts = pc.hostsByPreference;
    var pending = hosts.length;
    if (pending == 0) {
      return Future.error(const AgentException(AgentFailureKind.unreachable));
    }

    for (final host in hosts) {
      transport
          .probe(pc, host, token)
          .then(
            (info) {
              if (!completer.isCompleted) {
                completer.complete((host, info));
              }
            },
            onError: (Object error) {
              failures.add(error is AgentException ? error : const AgentException(AgentFailureKind.unreachable));
              if (--pending == 0 && !completer.isCompleted) {
                completer.completeError(_mostSignificant(failures));
              }
            },
          );
    }

    return completer.future;
  }

  static AgentException _mostSignificant(List<AgentException> failures) {
    for (final kind in [AgentFailureKind.certificateMismatch, AgentFailureKind.unauthorized]) {
      final match = failures.where((f) => f.kind == kind).firstOrNull;
      if (match != null) {
        return match;
      }
    }

    return failures.first;
  }

  void _registerEvents(AgentSession session, int generation) {
    session.hub.events('DevicePermissionsChanged').listen((arguments) {
      final current = state;
      if (generation != _generation || current is! OnlineConnection || arguments.isEmpty) {
        return;
      }

      final change = arguments.first! as Map<String, dynamic>;
      final info = current.session.info;
      final updated = AgentInfo(
        apiVersion: info.apiVersion,
        requiresPairing: false,
        agentVersion: info.agentVersion,
        pcName: info.pcName,
        deviceId: info.deviceId,
        role: deviceRoleFromJson(change['role']),
        permissions: {...(change['effectivePermissions'] as List<dynamic>).cast<String>()},
        tokenRotationDue: info.tokenRotationDue,
      );
      _session = current.session.withInfo(updated);
      state = OnlineConnection(_session!, current.since);
    });

    session.hub.events('DeviceRevoked').listen((_) {
      if (generation == _generation) {
        _generation++;
        unawaited(session.close());
        _session = null;
        state = RevokedConnection(session.pc);
      }
    });

    // O agente avisa antes de encerrar (Sair, logoff, desligamento); a queda que vem em seguida não é falha de rede.
    session.hub.events('AgentShuttingDown').listen((_) {
      if (generation == _generation) {
        _agentStopping = true;
      }
    });
  }

  void _onSessionClosed(int generation) {
    final current = state;
    if (generation != _generation || current is! OnlineConnection) {
      return;
    }

    _session = null;
    // Libera o REST e o HttpClient do Hub, que continuam abertos depois de uma queda.
    unawaited(current.session.close());
    _scheduleRetry(
      current.session.pc,
      generation,
      0,
      ref.read(clockProvider)(),
      lastInfo: current.session.info,
      agentStopped: _agentStopping,
    );
  }

  void _scheduleRetry(
    PairedPc pc,
    int generation,
    int attempt,
    DateTime offlineSince, {
    AgentInfo? lastInfo,
    bool? agentStopped,
  }) {
    final previous = state;
    state = ReconnectingConnection(
      pc,
      offlineSince: offlineSince,
      attempt: attempt + 1,
      lastInfo: lastInfo ?? (previous is ReconnectingConnection ? previous.lastInfo : null),
      agentStopped: agentStopped ?? (previous is ReconnectingConnection && previous.agentStopped),
    );

    final delays = ref.read(reconnectDelaysProvider);
    final base = delays[min(attempt, delays.length - 1)];
    final jitter = Duration(milliseconds: (base.inMilliseconds * 0.2 * _random.nextDouble()).round());
    _retry?.cancel();
    _retry = Timer(base + jitter, () => _connect(pc, generation, attempt: attempt + 1, offlineSince: offlineSince));
  }

  Future<void> _rememberHost(PairedPc pc, String host, AgentInfo info) async {
    if (!ref.mounted) {
      return;
    }

    final pcs = ref.read(pairedPcsProvider.notifier);
    final stored = ref.read(pairedPcsProvider).pcs.where((p) => p.id == pc.id).firstOrNull;
    if (stored != null && (stored.lastHost != host || stored.role != info.role)) {
      await pcs.update(stored.copyWith(lastHost: host, role: info.role, name: info.pcName));
    }
  }

  Future<void> _rotateToken(PairedPc pc, String host, String token) async {
    try {
      final rotated = await ref.read(agentTransportProvider).rotateToken(pc, host, token);
      if (ref.mounted) {
        await ref.read(pairedPcsProvider.notifier).saveToken(pc.id, rotated);
      }
    } on AgentException {
      // O token atual continua válido; a rotação é tentada de novo na próxima conexão.
    }
  }

  void _teardown(int generation) {
    if (generation != _generation) {
      return;
    }

    _generation++;
    _retry?.cancel();
    final session = _session;
    _session = null;
    if (session != null) {
      unawaited(session.close());
    }
  }
}
