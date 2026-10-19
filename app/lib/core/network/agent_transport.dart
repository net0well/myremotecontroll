import '../contracts/contracts.dart';
import '../errors/agent_exception.dart';
import '../storage/paired_pc.dart';
import 'agent_api.dart';
import 'agent_hub.dart';
import 'pin_registry.dart';

/// Sessão ativa com um PC: o endereço que respondeu, o que o agente informou e os canais REST e Hub.
class AgentSession {
  AgentSession({required this.pc, required this.host, required this.info, required this.api, required this.hub});

  final PairedPc pc;
  final String host;
  final AgentInfo info;
  final AgentApi api;
  final AgentHub hub;

  AgentSession withInfo(AgentInfo info) => AgentSession(pc: pc, host: host, info: info, api: api, hub: hub);

  Future<void> close() async {
    try {
      await hub.stop();
    } finally {
      api.close();
    }
  }
}

/// Como o app chega a um PC. A implementação real usa HTTPS e WebSocket com pinning; os testes usam uma falsa.
abstract interface class AgentTransport {
  /// `GET /api/v1/info` com o token: confirma que o PC responde, que o certificado é o pareado e que o token vale.
  Future<AgentInfo> probe(PairedPc pc, String host, String token);

  Future<AgentSession> open(
    PairedPc pc,
    String host,
    String token,
    AgentInfo info, {
    required void Function(Object? error) onClosed,
  });

  /// Pede um token novo (rotação a cada 30 dias, ADR-0008).
  Future<String> rotateToken(PairedPc pc, String host, String token);
}

class NetworkAgentTransport implements AgentTransport {
  NetworkAgentTransport(this._pins);

  final PinRegistry _pins;

  @override
  Future<AgentInfo> probe(PairedPc pc, String host, String token) async {
    final api = AgentApi(
      host: host,
      port: pc.port,
      pins: _pins,
      token: token,
      connectTimeout: const Duration(seconds: 4),
    );
    try {
      return await api.getInfo();
    } finally {
      api.close();
    }
  }

  @override
  Future<AgentSession> open(
    PairedPc pc,
    String host,
    String token,
    AgentInfo info, {
    required void Function(Object? error) onClosed,
  }) async {
    final api = AgentApi(host: host, port: pc.port, pins: _pins, token: token);
    final hub = SignalRAgentHub(host: host, port: pc.port, token: token, pins: _pins, onClosed: onClosed);
    try {
      await hub.start();
    } on Object {
      api.close();
      throw AgentException(
        _pins.hadMismatch(host, pc.port) ? AgentFailureKind.certificateMismatch : AgentFailureKind.unreachable,
      );
    }

    return AgentSession(pc: pc, host: host, info: info, api: api, hub: hub);
  }

  @override
  Future<String> rotateToken(PairedPc pc, String host, String token) async {
    final api = AgentApi(host: host, port: pc.port, pins: _pins, token: token);
    try {
      return await api.rotateToken();
    } finally {
      api.close();
    }
  }
}
