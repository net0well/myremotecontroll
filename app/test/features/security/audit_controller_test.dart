import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/contracts/contracts.dart';
import 'package:my_remote_controll/features/security/data/security_repository.dart';
import 'package:my_remote_controll/features/security/logic/security_controllers.dart';

AuditItem _item(int id) =>
    AuditItem(id: id, timestamp: DateTime.utc(2026, 10, 18, 12, id), action: 'devices.revoke', result: 'success');

class _FakeSecurityRepository implements SecurityRepository {
  final live = StreamController<AuditItem>.broadcast(sync: true);
  final Map<String?, Completer<AuditPage>> pages = {};

  Future<void> close() => live.close();

  /// Responde a página do [cursor], pedida antes ou depois desta chamada.
  void respond(String? cursor, AuditPage page) => (pages[cursor] ??= Completer<AuditPage>()).complete(page);

  @override
  Future<AuditPage> auditPage({String? cursor}) => (pages[cursor] ??= Completer<AuditPage>()).future;

  @override
  Stream<AuditItem> auditAppended() => live.stream;

  @override
  Future<List<DeviceInfo>> listDevices() => throw UnimplementedError();

  @override
  Future<DeviceInfo> changeRole(DeviceInfo device, DeviceRole role) => throw UnimplementedError();

  @override
  Future<void> revoke(String deviceId) => throw UnimplementedError();
}

void main() {
  late _FakeSecurityRepository repository;
  late ProviderContainer container;

  setUp(() {
    repository = _FakeSecurityRepository();
    container = ProviderContainer(overrides: [securityRepositoryProvider.overrideWithValue(repository)]);
    addTearDown(() async {
      container.dispose();
      await repository.close();
    });
  });

  List<int> ids() => container.read(auditControllerProvider).value!.items.map((i) => i.id).toList();

  test('registros novos chegam ao vivo (AuditAppended) e entram no topo', () async {
    final subscription = container.listen(auditControllerProvider, (_, _) {});
    addTearDown(subscription.close);
    repository.respond(null, AuditPage([_item(2), _item(1)], null));
    await container.read(auditControllerProvider.future);

    repository.live
      ..add(_item(3))
      ..add(_item(4));

    expect(ids(), [4, 3, 2, 1]);
  });

  test('o que chega durante a primeira página entra no topo sem repetir', () async {
    final subscription = container.listen(auditControllerProvider, (_, _) {});
    addTearDown(subscription.close);
    await Future<void>.delayed(Duration.zero);

    repository.live
      ..add(_item(2))
      ..add(_item(3));
    repository.respond(null, AuditPage([_item(2), _item(1)], null));
    await container.read(auditControllerProvider.future);

    expect(ids(), [3, 2, 1]);
  });

  test('registro ao vivo durante "carregar mais" não se perde', () async {
    final subscription = container.listen(auditControllerProvider, (_, _) {});
    addTearDown(subscription.close);
    repository.respond(null, AuditPage([_item(5), _item(4)], 'c1'));
    await container.read(auditControllerProvider.future);

    final loading = container.read(auditControllerProvider.notifier).loadMore();
    await Future<void>.delayed(Duration.zero);
    repository.live.add(_item(6));
    repository.respond('c1', AuditPage([_item(3)], null));
    await loading;

    expect(ids(), [6, 5, 4, 3]);
  });
}
