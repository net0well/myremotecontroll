import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/network/agent_connection.dart';
import '../data/security_repository.dart';

part 'security_controllers.g.dart';

/// Repositório da sessão online; nulo sem conexão.
@riverpod
SecurityRepository? securityRepository(Ref ref) {
  final connection = ref.watch(agentConnectionControllerProvider);
  return connection is OnlineConnection ? SessionSecurityRepository(connection.session) : null;
}

/// Permissões do aparelho no PC ativo, para esconder o que ele não pode fazer (spec 004, AC-7).
@riverpod
Set<String> currentPermissions(Ref ref) {
  final connection = ref.watch(agentConnectionControllerProvider);
  return connection is OnlineConnection ? connection.session.info.permissions : const {};
}

@riverpod
class DevicesController extends _$DevicesController {
  @override
  Future<List<DeviceInfo>> build() {
    final repository = ref.watch(securityRepositoryProvider);
    if (repository == null) {
      throw const AgentException(AgentFailureKind.unreachable);
    }

    return repository.listDevices();
  }

  Future<void> changeRole(DeviceInfo device, DeviceRole role) async {
    await ref.read(securityRepositoryProvider)!.changeRole(device, role);
    ref.invalidateSelf();
  }

  Future<void> revoke(DeviceInfo device) async {
    await ref.read(securityRepositoryProvider)!.revoke(device.id);
    ref.invalidateSelf();
  }
}

class AuditState {
  const AuditState(this.items, this.nextCursor, {this.loadingMore = false});

  final List<AuditItem> items;
  final String? nextCursor;
  final bool loadingMore;

  /// Registros novos, em ordem de chegada, vão para o topo (a lista vai do mais recente ao mais antigo); repetidos
  /// são ignorados.
  AuditState prepend(Iterable<AuditItem> arrivals) {
    final known = {for (final item in items) item.id};
    final fresh = arrivals.where((item) => known.add(item.id)).toList().reversed;
    return AuditState([...fresh, ...items], nextCursor, loadingMore: loadingMore);
  }
}

@riverpod
class AuditController extends _$AuditController {
  @override
  Future<AuditState> build() async {
    final repository = ref.watch(securityRepositoryProvider);
    if (repository == null) {
      throw const AgentException(AgentFailureKind.unreachable);
    }

    // Assina antes de carregar: o que chegar durante a primeira página entra no topo depois, sem repetir.
    final early = <AuditItem>[];
    AuditState? loaded;
    final subscription = repository.auditAppended().listen((item) {
      final current = loaded == null ? null : state.value;
      if (current == null) {
        early.add(item);
      } else {
        state = AsyncData(current.prepend([item]));
      }
    });
    ref.onDispose(subscription.cancel);

    final page = await repository.auditPage();
    return loaded = AuditState(page.items, page.nextCursor).prepend(early);
  }

  Future<void> loadMore() async {
    final current = state.value;
    final repository = ref.read(securityRepositoryProvider);
    if (current == null || current.nextCursor == null || current.loadingMore || repository == null) {
      return;
    }

    state = AsyncData(AuditState(current.items, current.nextCursor, loadingMore: true));
    final page = await repository.auditPage(cursor: current.nextCursor);
    // Relê o estado: registros ao vivo podem ter chegado durante a carga.
    final latest = state.value ?? current;
    state = AsyncData(AuditState([...latest.items, ...page.items], page.nextCursor));
  }
}
