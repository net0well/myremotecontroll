/// Espelho dos contratos do painel (`MyRemoteControll.Contracts.Dashboard`, ADR-0021).
library;

/// Ids dos cards do painel v1, na ordem da spec 007.
abstract final class DashboardCards {
  static const pc = 'pc';
  static const power = 'power';
  static const cpu = 'cpu';
  static const memory = 'memory';
  static const network = 'network';
  static const disks = 'disks';
  static const gpu = 'gpu';
  static const battery = 'battery';

  static const order = [pc, power, cpu, memory, network, disks, gpu, battery];
}

/// Ações rápidas do card de energia (`availableActions`).
abstract final class PowerCardActions {
  static const lock = 'power.lock';
  static const sleep = 'power.sleep';
  static const restart = 'power.restart';
  static const hibernate = 'power.hibernate';
  static const shutdown = 'power.shutdown';
  static const cancel = 'power.cancel';
}

/// Um card como o agente mandou: os dados dependem do tipo; as ações já vêm filtradas para este aparelho.
class CardState {
  const CardState({required this.cardId, required this.kind, required this.data, required this.availableActions});

  factory CardState.fromJson(Map<String, dynamic> json) => CardState(
    cardId: json['cardId'] as String,
    kind: json['kind'] as String,
    data: json['data'] is Map<String, dynamic> ? json['data'] as Map<String, dynamic> : const {},
    availableActions: {...((json['availableActions'] as List<dynamic>?) ?? const []).cast<String>()},
  );

  final String cardId;
  final String kind;

  /// Vazio quando a fonte não tem o dado (o card mostra "—").
  final Map<String, dynamic> data;
  final Set<String> availableActions;
}

class DashboardSnapshot {
  const DashboardSnapshot({required this.version, required this.at, required this.cards});

  factory DashboardSnapshot.fromJson(Map<String, dynamic> json) => DashboardSnapshot(
    version: json['version'] as int,
    at: DateTime.parse(json['at'] as String),
    cards: [for (final card in json['cards'] as List<dynamic>) CardState.fromJson(card as Map<String, dynamic>)],
  );

  final int version;
  final DateTime at;
  final List<CardState> cards;
}

/// Troca um card inteiro; [card] nulo = removido.
class CardPatch {
  const CardPatch(this.cardId, this.card);

  factory CardPatch.fromJson(Map<String, dynamic> json) => CardPatch(
    json['cardId'] as String,
    json['card'] == null ? null : CardState.fromJson(json['card'] as Map<String, dynamic>),
  );

  final String cardId;
  final CardState? card;
}

/// Evento `DashboardDelta(version, patches)`.
class DashboardDelta {
  const DashboardDelta(this.version, this.patches);

  factory DashboardDelta.fromArguments(List<Object?> arguments) => DashboardDelta(arguments[0]! as int, [
    for (final patch in arguments[1]! as List<dynamic>) CardPatch.fromJson(patch as Map<String, dynamic>),
  ]);

  final int version;
  final List<CardPatch> patches;
}

double? _double(Object? value) => (value as num?)?.toDouble();

int? _int(Object? value) => (value as num?)?.toInt();

/// Card de CPU: uso total, por núcleo (ausente nos dados reduzidos) e clock.
class CpuData {
  const CpuData({this.usagePercent, this.cores = const [], this.clockMhz});

  factory CpuData.fromJson(Map<String, dynamic> json) => CpuData(
    usagePercent: _double(json['usagePercent']),
    cores: [for (final core in (json['coreUsagePercent'] as List<dynamic>?) ?? const []) (core as num).toDouble()],
    clockMhz: _double(json['clockMhz']),
  );

  final double? usagePercent;
  final List<double> cores;
  final double? clockMhz;
}

class MemoryData {
  const MemoryData({this.usedBytes, this.totalBytes});

  factory MemoryData.fromJson(Map<String, dynamic> json) =>
      MemoryData(usedBytes: _int(json['usedBytes']), totalBytes: _int(json['totalBytes']));

  final int? usedBytes;
  final int? totalBytes;

  double? get usedFraction => usedBytes != null && (totalBytes ?? 0) > 0 ? usedBytes! / totalBytes! : null;
}

class NetworkInterfaceData {
  const NetworkInterfaceData({required this.name, required this.kind, required this.down, required this.up});

  factory NetworkInterfaceData.fromJson(Map<String, dynamic> json) => NetworkInterfaceData(
    name: json['name'] as String,
    kind: json['kind'] as String? ?? 'other',
    down: _int(json['downBytesPerSecond']) ?? 0,
    up: _int(json['upBytesPerSecond']) ?? 0,
  );

  final String name;

  /// `ethernet`, `wifi`, `tailscale`, `vpn` ou `other`.
  final String kind;
  final int down;
  final int up;
}

class NetworkData {
  const NetworkData({this.down, this.up, this.interfaces = const []});

  factory NetworkData.fromJson(Map<String, dynamic> json) => NetworkData(
    down: _int(json['downBytesPerSecond']),
    up: _int(json['upBytesPerSecond']),
    interfaces: [
      for (final item in (json['interfaces'] as List<dynamic>?) ?? const [])
        NetworkInterfaceData.fromJson(item as Map<String, dynamic>),
    ],
  );

  final int? down;
  final int? up;
  final List<NetworkInterfaceData> interfaces;
}

class DiskData {
  const DiskData({
    required this.drive,
    this.label,
    required this.usedBytes,
    required this.totalBytes,
    this.readBytesPerSecond,
    this.writeBytesPerSecond,
  });

  factory DiskData.fromJson(Map<String, dynamic> json) => DiskData(
    drive: json['drive'] as String,
    label: json['label'] as String?,
    usedBytes: _int(json['usedBytes']) ?? 0,
    totalBytes: _int(json['totalBytes']) ?? 0,
    readBytesPerSecond: _int(json['readBytesPerSecond']),
    writeBytesPerSecond: _int(json['writeBytesPerSecond']),
  );

  final String drive;
  final String? label;
  final int usedBytes;
  final int totalBytes;
  final int? readBytesPerSecond;
  final int? writeBytesPerSecond;

  double get usedFraction => totalBytes > 0 ? usedBytes / totalBytes : 0;
}

List<DiskData> disksFromJson(Map<String, dynamic> json) => [
  for (final disk in (json['disks'] as List<dynamic>?) ?? const []) DiskData.fromJson(disk as Map<String, dynamic>),
];

class GpuData {
  const GpuData({this.name, this.usagePercent, this.dedicatedUsedBytes, this.dedicatedTotalBytes});

  factory GpuData.fromJson(Map<String, dynamic> json) => GpuData(
    name: json['name'] as String?,
    usagePercent: _double(json['usagePercent']),
    dedicatedUsedBytes: _int(json['dedicatedUsedBytes']),
    dedicatedTotalBytes: _int(json['dedicatedTotalBytes']),
  );

  final String? name;
  final double? usagePercent;
  final int? dedicatedUsedBytes;
  final int? dedicatedTotalBytes;
}

class BatteryData {
  const BatteryData({required this.percent, required this.charging, this.secondsRemaining});

  factory BatteryData.fromJson(Map<String, dynamic> json) => BatteryData(
    percent: _int(json['percent']) ?? 0,
    charging: json['charging'] as bool? ?? false,
    secondsRemaining: _int(json['secondsRemaining']),
  );

  final int percent;
  final bool charging;
  final int? secondsRemaining;
}

class AddressData {
  const AddressData({required this.interfaceName, required this.kind, required this.address});

  factory AddressData.fromJson(Map<String, dynamic> json) => AddressData(
    interfaceName: json['interfaceName'] as String,
    kind: json['kind'] as String? ?? 'other',
    address: json['address'] as String,
  );

  final String interfaceName;
  final String kind;
  final String address;
}

class PcData {
  const PcData({
    required this.hostname,
    required this.osName,
    required this.osVersion,
    this.bootedAt,
    required this.userName,
    this.addresses = const [],
    this.onAcPower = true,
    this.powerPlan,
  });

  factory PcData.fromJson(Map<String, dynamic> json) => PcData(
    hostname: json['hostname'] as String? ?? '',
    osName: json['osName'] as String? ?? '',
    osVersion: json['osVersion'] as String? ?? '',
    bootedAt: json['bootedAt'] == null ? null : DateTime.parse(json['bootedAt'] as String),
    userName: json['userName'] as String? ?? '',
    addresses: [
      for (final item in (json['addresses'] as List<dynamic>?) ?? const [])
        AddressData.fromJson(item as Map<String, dynamic>),
    ],
    onAcPower: json['onAcPower'] as bool? ?? true,
    powerPlan: json['powerPlan'] as String?,
  );

  final String hostname;
  final String osName;
  final String osVersion;
  final DateTime? bootedAt;
  final String userName;
  final List<AddressData> addresses;
  final bool onAcPower;
  final String? powerPlan;
}

/// Ação de energia (`PowerActionKind`), com o id da ação rápida correspondente.
enum PowerActionKind {
  lock(PowerCardActions.lock),
  sleep(PowerCardActions.sleep),
  restart(PowerCardActions.restart),
  hibernate(PowerCardActions.hibernate),
  shutdown(PowerCardActions.shutdown);

  const PowerActionKind(this.actionId);

  final String actionId;

  /// Desligar, reiniciar e hibernar pedem deslizar + biometria e a permissão `power.shutdown` (spec 005).
  bool get destructive => this == restart || this == hibernate || this == shutdown;

  /// Só desligar e reiniciar fecham os apps (a opção "forçar" vale para eles).
  bool get closesApps => this == restart || this == shutdown;

  static PowerActionKind fromJson(Object? value) => values.firstWhere((v) => v.name == value, orElse: () => lock);
}

class PowerCountdown {
  const PowerCountdown({
    required this.action,
    required this.executesAt,
    required this.force,
    this.requestedByDeviceName,
    this.updatePending = false,
  });

  factory PowerCountdown.fromJson(Map<String, dynamic> json) => PowerCountdown(
    action: PowerActionKind.fromJson(json['action']),
    executesAt: DateTime.parse(json['executesAt'] as String),
    force: json['force'] as bool? ?? false,
    requestedByDeviceName: json['requestedByDeviceName'] as String?,
    updatePending: json['updatePending'] as bool? ?? false,
  );

  final PowerActionKind action;
  final DateTime executesAt;
  final bool force;
  final String? requestedByDeviceName;
  final bool updatePending;
}

class PowerData {
  const PowerData({
    this.pending,
    this.sleepAvailable = true,
    this.hibernateAvailable = true,
    this.updatePending = false,
  });

  factory PowerData.fromJson(Map<String, dynamic> json) => PowerData(
    pending: json['pending'] == null ? null : PowerCountdown.fromJson(json['pending'] as Map<String, dynamic>),
    sleepAvailable: json['sleepAvailable'] as bool? ?? true,
    hibernateAvailable: json['hibernateAvailable'] as bool? ?? true,
    updatePending: json['updatePending'] as bool? ?? false,
  );

  final PowerCountdown? pending;
  final bool sleepAvailable;
  final bool hibernateAvailable;
  final bool updatePending;
}
