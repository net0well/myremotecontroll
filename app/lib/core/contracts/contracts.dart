/// Espelho dos contratos do agente (`MyRemoteControll.Contracts`). Enums trafegam como texto camelCase.
library;

enum DevicePlatform { android, ios }

/// Papéis da seção 9 do documento base (ADR-0009).
enum DeviceRole { readOnly, operator, administrator }

T _enumByName<T extends Enum>(List<T> values, Object? name, T fallback) =>
    values.firstWhere((v) => v.name == name, orElse: () => fallback);

DeviceRole deviceRoleFromJson(Object? value) => _enumByName(DeviceRole.values, value, DeviceRole.readOnly);

DevicePlatform devicePlatformFromJson(Object? value) =>
    _enumByName(DevicePlatform.values, value, DevicePlatform.android);

/// Chaves de permissão (seção 9). O app só esconde o que não pode; quem decide é o agente.
abstract final class PermissionKeys {
  static const statusView = 'status.view';
  static const monitorView = 'monitor.view';
  static const powerBasic = 'power.basic';
  static const powerShutdown = 'power.shutdown';
  static const securityViewAudit = 'security.viewAudit';
  static const securityManageDevices = 'security.manageDevices';
  static const securityPanic = 'security.panic';
}

/// Códigos de erro estáveis (hub-contract.md, seção 6).
abstract final class ErrorCodes {
  static const agentPaused = 'agent.paused';
  static const validationFailed = 'validation.failed';
  static const authUnauthorized = 'auth.unauthorized';
  static const authRevoked = 'auth.revoked';
  static const authForbidden = 'auth.forbidden';
  static const pairingInvalidCode = 'pairing.invalidCode';
  static const pairingExpired = 'pairing.expired';
  static const pairingConsumed = 'pairing.consumed';
  static const pairingRejected = 'pairing.rejected';
  static const pairingConfirmationTimeout = 'pairing.confirmationTimeout';
  static const pairingLocked = 'pairing.locked';
  static const securityLastAdmin = 'security.lastAdmin';
  static const powerAlreadyScheduled = 'power.alreadyScheduled';
  static const powerActiveWork = 'power.activeWork';
  static const powerHibernateUnavailable = 'power.hibernateUnavailable';
  static const powerSleepUnavailable = 'power.sleepUnavailable';
  static const powerNothingToCancel = 'power.nothingToCancel';

  /// Só do app: a chamada não chegou ao agente (conexão caiu no meio).
  static const connectionLost = 'connection.lost';
}

/// `GET /api/v1/info`.
class AgentInfo {
  const AgentInfo({
    required this.apiVersion,
    required this.requiresPairing,
    this.agentVersion,
    this.pcName,
    this.deviceId,
    this.role,
    this.permissions = const {},
    this.tokenRotationDue = false,
  });

  factory AgentInfo.fromJson(Map<String, dynamic> json) => AgentInfo(
    apiVersion: json['apiVersion'] as String,
    requiresPairing: json['requiresPairing'] as bool,
    agentVersion: json['agentVersion'] as String?,
    pcName: json['pcName'] as String?,
    deviceId: json['deviceId'] as String?,
    role: json['role'] == null ? null : deviceRoleFromJson(json['role']),
    permissions: {...((json['permissions'] as List<dynamic>?) ?? const []).cast<String>()},
    tokenRotationDue: json['tokenRotationDue'] as bool? ?? false,
  );

  final String apiVersion;
  final bool requiresPairing;
  final String? agentVersion;
  final String? pcName;
  final String? deviceId;
  final DeviceRole? role;
  final Set<String> permissions;
  final bool tokenRotationDue;

  bool can(String permission) => permissions.contains(permission);
}

/// Retorno de todo método do Hub: `{ ok, data }` ou `{ ok: false, error: { code, message, details? } }`.
class HubResult<T> {
  const HubResult.success(T this.data) : ok = true, errorCode = null, errorMessage = null, errorDetails = null;

  const HubResult.failure(String this.errorCode, String this.errorMessage, {this.errorDetails})
    : ok = false,
      data = null;

  factory HubResult.fromJson(Object? json, T Function(Object? data) parse) {
    final map = json as Map<String, dynamic>;
    if (map['ok'] == true) {
      return HubResult.success(parse(map['data']));
    }

    final error = map['error'] as Map<String, dynamic>? ?? const {};
    return HubResult.failure(
      error['code'] as String? ?? 'internal.error',
      error['message'] as String? ?? '',
      errorDetails: error['details'],
    );
  }

  final bool ok;
  final T? data;
  final String? errorCode;
  final String? errorMessage;

  /// Dados do erro quando o código os prevê (ex.: a lista de trabalho ativo de `power.activeWork`).
  final Object? errorDetails;
}
