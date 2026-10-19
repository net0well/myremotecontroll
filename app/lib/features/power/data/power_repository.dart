import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/contracts/contracts.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/network/agent_transport.dart';
import '../../dashboard/data/dashboard_models.dart';

part 'power_repository.g.dart';

/// Pedido de ação de energia (`PowerActionRequest`, spec 005).
class PowerRequest {
  const PowerRequest({
    required this.action,
    required this.delaySeconds,
    this.force = false,
    this.acknowledgeActiveWork = false,
    this.localConfirmation = false,
  });

  final PowerActionKind action;
  final int delaySeconds;
  final bool force;
  final bool acknowledgeActiveWork;

  /// O usuário confirmou com biometria/PIN no aparelho (vai para a auditoria do agente).
  final bool localConfirmation;

  PowerRequest acknowledged() => PowerRequest(
    action: action,
    delaySeconds: delaySeconds,
    force: force,
    acknowledgeActiveWork: true,
    localConfirmation: localConfirmation,
  );

  Map<String, Object> toJson() => {
    'action': action.name,
    'delaySeconds': delaySeconds,
    'force': force,
    'acknowledgeActiveWork': acknowledgeActiveWork,
    'localConfirmation': localConfirmation,
  };
}

/// Item de `power.activeWork`: sessão do Claude trabalhando, build rodando…
class ActiveWorkItem {
  const ActiveWorkItem(this.kind, this.description);

  final String kind;
  final String description;

  static List<ActiveWorkItem> fromDetails(Object? details) => [
    for (final item in details is List ? details : const [])
      if (item is Map<String, dynamic>)
        ActiveWorkItem(item['kind'] as String? ?? '', item['description'] as String? ?? ''),
  ];
}

/// Por que uma contagem terminou sem executar (`PowerCancelled`).
enum PowerCancelSource { device, tray, agentShutdown, failed }

class PowerCancelled {
  const PowerCancelled(this.action, this.source);

  factory PowerCancelled.fromArguments(List<Object?> arguments) {
    final json = arguments.first! as Map<String, dynamic>;
    return PowerCancelled(
      PowerActionKind.fromJson(json['action']),
      PowerCancelSource.values.firstWhere((s) => s.name == json['source'], orElse: () => PowerCancelSource.device),
    );
  }

  final PowerActionKind action;
  final PowerCancelSource source;
}

/// Ações de energia sobre a sessão ativa. Devolve o [HubResult] inteiro: a tela decide pelo código de erro.
class PowerRepository {
  PowerRepository(this._session);

  final AgentSession _session;

  Future<HubResult<PowerCountdown>> request(PowerRequest request) => _session.hub.invoke('PowerAction', [
    request.toJson(),
  ], (data) => PowerCountdown.fromJson(data! as Map<String, dynamic>));

  Future<HubResult<void>> cancel() => _session.hub.invoke('CancelPowerAction', const [], (_) {});

  Stream<PowerCancelled> cancellations() => _session.hub.events('PowerCancelled').map(PowerCancelled.fromArguments);
}

/// Repositório da sessão online; nulo sem conexão.
@riverpod
PowerRepository? powerRepository(Ref ref) {
  final session = ref.watch(agentConnectionControllerProvider.select((c) => c is OnlineConnection ? c.session : null));
  return session == null ? null : PowerRepository(session);
}

/// Contagens que terminaram sem executar, para avisar na tela (cancelada no PC, falha do Windows…).
@riverpod
Stream<PowerCancelled> powerCancellations(Ref ref) =>
    ref.watch(powerRepositoryProvider)?.cancellations() ?? const Stream.empty();
