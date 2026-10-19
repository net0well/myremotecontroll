import '../../../core/contracts/contracts.dart';
import '../../../core/errors/agent_exception.dart';
import '../../../core/network/agent_transport.dart';

/// Aparelho pareado com o PC (`DeviceDto`).
class DeviceInfo {
  const DeviceInfo({
    required this.id,
    required this.name,
    required this.platform,
    required this.role,
    required this.extraPermissions,
    required this.connected,
    required this.revoked,
    this.lastSeenAt,
  });

  factory DeviceInfo.fromJson(Map<String, dynamic> json) => DeviceInfo(
    id: json['id'] as String,
    name: json['name'] as String,
    platform: devicePlatformFromJson(json['platform']),
    role: deviceRoleFromJson(json['role']),
    extraPermissions: (json['extraPermissions'] as List<dynamic>).cast<String>(),
    connected: json['connected'] as bool,
    revoked: json['revoked'] as bool,
    lastSeenAt: json['lastSeenAt'] == null ? null : DateTime.parse(json['lastSeenAt'] as String),
  );

  final String id;
  final String name;
  final DevicePlatform platform;
  final DeviceRole role;
  final List<String> extraPermissions;
  final bool connected;
  final bool revoked;
  final DateTime? lastSeenAt;
}

/// Registro de auditoria (`AuditEntryDto`).
class AuditItem {
  const AuditItem({
    required this.id,
    required this.timestamp,
    required this.action,
    required this.result,
    this.deviceName,
    this.errorCode,
  });

  factory AuditItem.fromJson(Map<String, dynamic> json) => AuditItem(
    id: json['id'] as int,
    timestamp: DateTime.parse(json['timestamp'] as String),
    action: json['action'] as String,
    result: json['result'] as String,
    deviceName: json['deviceName'] as String?,
    errorCode: json['errorCode'] as String?,
  );

  final int id;
  final DateTime timestamp;
  final String action;
  final String result;
  final String? deviceName;
  final String? errorCode;
}

class AuditPage {
  const AuditPage(this.items, this.nextCursor);

  final List<AuditItem> items;
  final String? nextCursor;
}

/// Segurança → aparelhos e auditoria (spec 003), sobre a sessão ativa.
abstract interface class SecurityRepository {
  Future<List<DeviceInfo>> listDevices();

  Future<DeviceInfo> changeRole(DeviceInfo device, DeviceRole role);

  Future<void> revoke(String deviceId);

  Future<AuditPage> auditPage({String? cursor});

  /// Registros novos ao vivo (`AuditAppended`, só para quem tem `security.viewAudit`).
  Stream<AuditItem> auditAppended();
}

class SessionSecurityRepository implements SecurityRepository {
  SessionSecurityRepository(this._session);

  final AgentSession _session;

  @override
  Future<List<DeviceInfo>> listDevices() async {
    final result = await _session.hub.invoke(
      'ListDevices',
      const [],
      (data) => (data! as List<dynamic>).map((d) => DeviceInfo.fromJson(d as Map<String, dynamic>)).toList(),
    );
    return _unwrap(result);
  }

  @override
  Future<DeviceInfo> changeRole(DeviceInfo device, DeviceRole role) async {
    final result = await _session.hub.invoke('UpdateDevicePermissions', [
      device.id,
      {'role': role.name, 'extraPermissions': device.extraPermissions},
    ], (data) => DeviceInfo.fromJson(data! as Map<String, dynamic>));
    return _unwrap(result);
  }

  @override
  Future<void> revoke(String deviceId) => _session.api.revokeDevice(deviceId);

  @override
  Future<AuditPage> auditPage({String? cursor}) async {
    final json = await _session.api.getAudit(cursor: cursor);
    return AuditPage(
      (json['items'] as List<dynamic>).map((i) => AuditItem.fromJson(i as Map<String, dynamic>)).toList(),
      json['nextCursor'] as String?,
    );
  }

  @override
  Stream<AuditItem> auditAppended() => _session.hub
      .events('AuditAppended')
      .where((arguments) => arguments.isNotEmpty)
      .map((arguments) => AuditItem.fromJson(arguments.first! as Map<String, dynamic>));

  static T _unwrap<T>(HubResult<T> result) {
    if (result.ok) {
      return result.data as T;
    }

    throw AgentException(AgentFailureKind.rejected, code: result.errorCode, serverMessage: result.errorMessage);
  }
}
