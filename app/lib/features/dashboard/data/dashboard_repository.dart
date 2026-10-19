import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/network/agent_transport.dart';
import 'dashboard_models.dart';

/// Painel em tempo real (ADR-0021) sobre a sessão ativa: assinatura, snapshot, ritmo e deltas.
class DashboardRepository {
  DashboardRepository(this._session);

  final AgentSession _session;

  /// Deltas desta conexão, na ordem em que chegam.
  Stream<DashboardDelta> deltas() => _session.hub.events('DashboardDelta').map(DashboardDelta.fromArguments);

  Future<DashboardSnapshot> subscribe({required int intervalMs, required bool lowData}) =>
      _snapshot('SubscribeDashboard', [
        {'metricsIntervalMs': intervalMs, 'includeScreenThumbnail': false, 'lowDataMode': lowData},
      ]);

  Future<DashboardSnapshot> requestSnapshot() => _snapshot('RequestDashboardSnapshot', const []);

  Future<void> unsubscribe() async => _unwrap(await _session.hub.invoke('UnsubscribeDashboard', const [], (_) {}));

  Future<DashboardSnapshot> _snapshot(String method, List<Object> arguments) async => _unwrap(
    await _session.hub.invoke(method, arguments, (data) => DashboardSnapshot.fromJson(data! as Map<String, dynamic>)),
  );

  static T _unwrap<T>(HubResult<T> result) {
    if (result.ok) {
      return result.data as T;
    }

    throw AgentException(AgentFailureKind.rejected, code: result.errorCode, serverMessage: result.errorMessage);
  }
}
