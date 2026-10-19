import '../contracts/contracts.dart';

/// PC pareado neste aparelho. O token fica à parte, no armazenamento seguro.
///
/// O [id] é o pin do certificado do agente: identifica o PC mesmo que dois PCs tenham o mesmo nome.
class PairedPc {
  const PairedPc({
    required this.id,
    required this.name,
    required this.hosts,
    required this.port,
    required this.pin,
    required this.deviceId,
    required this.role,
    this.alias,
    this.lastHost,
  });

  factory PairedPc.fromJson(Map<String, dynamic> json) => PairedPc(
    id: json['id'] as String,
    name: json['name'] as String,
    hosts: (json['hosts'] as List<dynamic>).cast<String>(),
    port: json['port'] as int,
    pin: json['pin'] as String,
    deviceId: json['deviceId'] as String,
    role: deviceRoleFromJson(json['role']),
    alias: json['alias'] as String?,
    lastHost: json['lastHost'] as String?,
  );

  final String id;
  final String name;
  final List<String> hosts;
  final int port;
  final String pin;
  final String deviceId;
  final DeviceRole role;
  final String? alias;

  /// Último endereço que respondeu; tentado primeiro na próxima conexão.
  final String? lastHost;

  String get displayName => alias?.isNotEmpty == true ? alias! : name;

  /// Hosts na ordem de tentativa: o último que funcionou primeiro.
  List<String> get hostsByPreference => [?lastHost, ...hosts.where((h) => h != lastHost)];

  PairedPc copyWith({String? alias, String? lastHost, DeviceRole? role, String? name}) => PairedPc(
    id: id,
    name: name ?? this.name,
    hosts: hosts,
    port: port,
    pin: pin,
    deviceId: deviceId,
    role: role ?? this.role,
    alias: alias ?? this.alias,
    lastHost: lastHost ?? this.lastHost,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'hosts': hosts,
    'port': port,
    'pin': pin,
    'deviceId': deviceId,
    'role': role.name,
    'alias': ?alias,
    'lastHost': ?lastHost,
  };
}
