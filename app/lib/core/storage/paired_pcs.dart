import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../providers.dart';
import 'paired_pc.dart';

part 'paired_pcs.g.dart';

class PairedPcsState {
  const PairedPcsState({this.pcs = const [], this.activeId});

  final List<PairedPc> pcs;
  final String? activeId;

  PairedPc? get active => pcs.where((pc) => pc.id == activeId).firstOrNull;
}

/// PCs pareados (preferências) e seus tokens (armazenamento seguro), com o PC ativo.
@Riverpod(keepAlive: true)
class PairedPcs extends _$PairedPcs {
  static const _pcsKey = 'paired_pcs.v1';
  static const _activeKey = 'active_pc.v1';

  static String tokenKey(String pcId) => 'token:$pcId';

  @override
  PairedPcsState build() {
    final store = ref.watch(keyValueStoreProvider);
    final raw = store.getString(_pcsKey);
    final pcs = raw == null
        ? <PairedPc>[]
        : (jsonDecode(raw) as List<dynamic>).map((e) => PairedPc.fromJson(e as Map<String, dynamic>)).toList();

    final pins = ref.watch(pinRegistryProvider);
    for (final pc in pcs) {
      pins.register(pc.hosts, pc.port, pc.pin);
    }

    final activeId = store.getString(_activeKey);
    return PairedPcsState(pcs: pcs, activeId: pcs.any((pc) => pc.id == activeId) ? activeId : pcs.firstOrNull?.id);
  }

  Future<String?> tokenOf(String pcId) => ref.read(secretStoreProvider).read(tokenKey(pcId));

  /// Salva um PC recém-pareado (substitui o mesmo PC, se já existia) e o torna ativo.
  Future<void> add(PairedPc pc, String token) async {
    await ref.read(secretStoreProvider).write(tokenKey(pc.id), token);
    ref.read(pinRegistryProvider).register(pc.hosts, pc.port, pc.pin);
    await _save(PairedPcsState(pcs: [...state.pcs.where((p) => p.id != pc.id), pc], activeId: pc.id));
  }

  Future<void> saveToken(String pcId, String token) => ref.read(secretStoreProvider).write(tokenKey(pcId), token);

  Future<void> setActive(String pcId) => _save(PairedPcsState(pcs: state.pcs, activeId: pcId));

  Future<void> update(PairedPc pc) =>
      _save(PairedPcsState(pcs: [for (final p in state.pcs) p.id == pc.id ? pc : p], activeId: state.activeId));

  Future<void> remove(String pcId) async {
    final pc = state.pcs.where((p) => p.id == pcId).firstOrNull;
    await ref.read(secretStoreProvider).delete(tokenKey(pcId));
    if (pc != null) {
      ref.read(pinRegistryProvider).unregisterPin(pc.pin);
    }

    final remaining = state.pcs.where((p) => p.id != pcId).toList();
    await _save(
      PairedPcsState(pcs: remaining, activeId: state.activeId == pcId ? remaining.firstOrNull?.id : state.activeId),
    );
  }

  Future<void> _save(PairedPcsState next) async {
    final store = ref.read(keyValueStoreProvider);
    await store.setString(_pcsKey, jsonEncode(next.pcs.map((pc) => pc.toJson()).toList()));
    if (next.activeId == null) {
      await store.remove(_activeKey);
    } else {
      await store.setString(_activeKey, next.activeId!);
    }

    if (ref.mounted) {
      state = next;
    }
  }
}

/// Só o ID do PC ativo: mudar dados do PC (ex.: último host) não deve derrubar a conexão.
@Riverpod(keepAlive: true)
String? activePcId(Ref ref) => ref.watch(pairedPcsProvider.select((s) => s.active?.id));
