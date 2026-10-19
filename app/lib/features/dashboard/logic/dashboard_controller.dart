import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/errors/agent_exception.dart';
import '../../../core/network/agent_connection.dart';
import '../../../core/network/agent_hub.dart';
import '../../../core/network/agent_transport.dart';
import '../../../core/platform/data_saver.dart';
import '../data/dashboard_models.dart';
import '../data/dashboard_repository.dart';

part 'dashboard_controller.g.dart';

/// Intervalo normal e econômico do painel (spec 007).
const dashboardIntervalMs = 1000;
const dashboardConstrainedIntervalMs = 3000;

/// Quantos pontos as sparklines guardam (60 s no ritmo normal).
const historyLength = 60;

/// Séries das sparklines, acumuladas no app a partir dos deltas.
abstract final class HistorySeries {
  static const cpu = 'cpu';
  static const memory = 'memory';
  static const networkDown = 'network.down';
  static const gpu = 'gpu';
}

class DashboardState {
  const DashboardState({
    this.pcId,
    this.cards = const {},
    this.history = const {},
    this.version = 0,
    this.live = false,
  });

  final String? pcId;

  /// Último estado conhecido de cada card; continua aqui com o PC offline (AC-4).
  final Map<String, CardState> cards;
  final Map<String, List<double>> history;
  final int version;

  /// Recebendo deltas agora (falso offline, em segundo plano ou antes do primeiro snapshot).
  final bool live;

  DashboardState copyWith({
    Map<String, CardState>? cards,
    Map<String, List<double>>? history,
    int? version,
    bool? live,
  }) => DashboardState(
    pcId: pcId,
    cards: cards ?? this.cards,
    history: history ?? this.history,
    version: version ?? this.version,
    live: live ?? this.live,
  );
}

@Riverpod(keepAlive: true)
DataSaver dataSaver(Ref ref) => PlatformDataSaver();

/// Painel do PC ativo (ADR-0021): assina ao conectar, aplica os deltas em ordem e pede snapshot quando a versão pula
/// (AC-3). Cada card muda só quando o seu pedaço muda, então só ele reconstrói (AC-5).
@Riverpod(keepAlive: true)
class DashboardController extends _$DashboardController {
  DashboardRepository? _repository;
  AgentHub? _hub;
  StreamSubscription<DashboardDelta>? _deltas;
  StreamSubscription<void>? _networkChanges;
  int _generation = 0;
  bool _paused = false;
  bool _resyncing = false;
  final List<DashboardDelta> _buffered = [];

  @override
  DashboardState build() {
    ref.onDispose(() {
      _detach();
      unawaited(_networkChanges?.cancel());
    });
    ref.listen(agentConnectionControllerProvider, (_, next) => _onConnection(next));
    _networkChanges = ref.read(dataSaverProvider).changes.listen((_) => _resubscribe());

    final connection = ref.read(agentConnectionControllerProvider);
    scheduleMicrotask(() => _onConnection(connection));
    return const DashboardState();
  }

  /// App em segundo plano: cancela a assinatura e guarda o último estado.
  Future<void> pause() async {
    _paused = true;
    final repository = _repository;
    _detach();
    if (ref.mounted) {
      state = state.copyWith(live: false);
    }

    if (repository != null) {
      try {
        await repository.unsubscribe();
      } on AgentException {
        // A conexão pode já ter caído; o agente tira a inscrição ao desconectar.
      }
    }
  }

  /// App de volta: assina de novo e recebe um snapshot completo.
  void resume() {
    _paused = false;
    _onConnection(ref.read(agentConnectionControllerProvider));
  }

  void _onConnection(AgentConnection connection) {
    if (!ref.mounted) {
      return;
    }

    final pcId = switch (connection) {
      OnlineConnection(:final session) => session.pc.id,
      ConnectingConnection(:final pc) || ReconnectingConnection(:final pc) => pc.id,
      RevokedConnection(:final pc) || CertificateMismatchConnection(:final pc) => pc.id,
      NoPcConnection() => null,
    };

    // Outro PC: nada do anterior pode aparecer.
    if (pcId != state.pcId) {
      _detach();
      state = DashboardState(pcId: pcId);
    }

    if (connection is OnlineConnection && !_paused) {
      // Mudança de permissões cria outra sessão sobre o mesmo Hub: a assinatura continua valendo.
      if (_repository == null || _hub != connection.session.hub) {
        unawaited(_attach(connection.session));
      }
    } else if (connection is! OnlineConnection) {
      _detach();
      state = state.copyWith(live: false);
    }
  }

  Future<void> _attach(AgentSession session) async {
    _detach();
    final generation = ++_generation;
    _hub = session.hub;
    final repository = _repository = DashboardRepository(session);
    _deltas = repository.deltas().listen((delta) => _onDelta(generation, delta));
    await _subscribe(generation, repository);
  }

  void _detach() {
    _generation++;
    unawaited(_deltas?.cancel());
    _deltas = null;
    _repository = null;
    _hub = null;
    _resyncing = false;
    _buffered.clear();
  }

  bool _current(int generation) => ref.mounted && generation == _generation;

  Future<void> _resubscribe() async {
    final repository = _repository;
    if (repository != null && !_paused) {
      await _subscribe(_generation, repository);
    }
  }

  Future<void> _subscribe(int generation, DashboardRepository repository) async {
    final constrained = await ref.read(dataSaverProvider).isConstrained();
    if (!_current(generation)) {
      return;
    }

    await _loadSnapshot(
      generation,
      () => repository.subscribe(
        intervalMs: constrained ? dashboardConstrainedIntervalMs : dashboardIntervalMs,
        lowData: constrained,
      ),
    );
  }

  /// Snapshot com os deltas que chegam no meio guardados e aplicados depois, se forem mais novos.
  Future<void> _loadSnapshot(int generation, Future<DashboardSnapshot> Function() load) async {
    _resyncing = true;
    _buffered.clear();
    try {
      final snapshot = await load();
      if (!_current(generation)) {
        return;
      }

      _applySnapshot(snapshot);
    } on AgentException {
      // Sem resposta: a queda da conexão é tratada pelo AgentConnectionController.
      return;
    } finally {
      if (_current(generation)) {
        _resyncing = false;
      }
    }

    final pending = [..._buffered]..sort((a, b) => a.version.compareTo(b.version));
    _buffered.clear();
    for (final delta in pending) {
      _applyDelta(generation, delta);
    }
  }

  void _onDelta(int generation, DashboardDelta delta) {
    if (!_current(generation)) {
      return;
    }

    if (_resyncing) {
      _buffered.add(delta);
      return;
    }

    _applyDelta(generation, delta);
  }

  void _applyDelta(int generation, DashboardDelta delta) {
    if (delta.version <= state.version) {
      return; // Já coberto por um snapshot mais novo.
    }

    if (delta.version != state.version + 1) {
      // Pulou versão: o estado local pode estar errado; um snapshot novo resolve (AC-3).
      final repository = _repository;
      if (repository != null && !_resyncing) {
        unawaited(_loadSnapshot(generation, repository.requestSnapshot));
      }

      return;
    }

    final cards = {...state.cards};
    var history = state.history;
    for (final patch in delta.patches) {
      final card = patch.card;
      if (card == null) {
        cards.remove(patch.cardId);
      } else {
        cards[patch.cardId] = card;
        history = _record(history, card);
      }
    }

    state = state.copyWith(cards: cards, history: history, version: delta.version, live: true);
  }

  void _applySnapshot(DashboardSnapshot snapshot) {
    var history = state.history;
    for (final card in snapshot.cards) {
      history = _record(history, card);
    }

    state = state.copyWith(
      cards: {for (final card in snapshot.cards) card.cardId: card},
      history: history,
      version: snapshot.version,
      live: true,
    );
  }

  /// Acrescenta o valor do card à sua série; as outras séries continuam as mesmas listas (sem rebuild).
  static Map<String, List<double>> _record(Map<String, List<double>> history, CardState card) {
    final (series, value) = switch (card.cardId) {
      DashboardCards.cpu => (HistorySeries.cpu, CpuData.fromJson(card.data).usagePercent),
      DashboardCards.memory => (HistorySeries.memory, MemoryData.fromJson(card.data).usedFraction?.toPercent()),
      DashboardCards.network => (HistorySeries.networkDown, NetworkData.fromJson(card.data).down?.toDouble()),
      DashboardCards.gpu => (HistorySeries.gpu, GpuData.fromJson(card.data).usagePercent),
      _ => (null, null),
    };
    if (series == null || value == null) {
      return history;
    }

    final values = [...?history[series], value];
    return {...history, series: values.length > historyLength ? values.sublist(values.length - historyLength) : values};
  }
}

extension on double {
  double toPercent() => this * 100;
}

/// Um card do painel; só notifica quando esse card muda (AC-5).
@riverpod
CardState? dashboardCard(Ref ref, String cardId) =>
    ref.watch(dashboardControllerProvider.select((state) => state.cards[cardId]));

/// Série de uma sparkline (últimos 60 valores).
@riverpod
List<double> dashboardHistory(Ref ref, String series) =>
    ref.watch(dashboardControllerProvider.select((state) => state.history[series])) ?? const [];
