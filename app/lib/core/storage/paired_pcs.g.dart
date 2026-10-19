// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'paired_pcs.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// PCs pareados (preferências) e seus tokens (armazenamento seguro), com o PC ativo.

@ProviderFor(PairedPcs)
final pairedPcsProvider = PairedPcsProvider._();

/// PCs pareados (preferências) e seus tokens (armazenamento seguro), com o PC ativo.
final class PairedPcsProvider extends $NotifierProvider<PairedPcs, PairedPcsState> {
  /// PCs pareados (preferências) e seus tokens (armazenamento seguro), com o PC ativo.
  PairedPcsProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'pairedPcsProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$pairedPcsHash();

  @$internal
  @override
  PairedPcs create() => PairedPcs();

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PairedPcsState value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<PairedPcsState>(value));
  }
}

String _$pairedPcsHash() => r'50fb0b1a8669a42856299f3845247b664959b16e';

/// PCs pareados (preferências) e seus tokens (armazenamento seguro), com o PC ativo.

abstract class _$PairedPcs extends $Notifier<PairedPcsState> {
  PairedPcsState build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<PairedPcsState, PairedPcsState>;
    final element =
        ref.element
            as $ClassProviderElement<AnyNotifier<PairedPcsState, PairedPcsState>, PairedPcsState, Object?, Object?>;
    return element.handleCreate(ref, build);
  }
}

/// Só o ID do PC ativo: mudar dados do PC (ex.: último host) não deve derrubar a conexão.

@ProviderFor(activePcId)
final activePcIdProvider = ActivePcIdProvider._();

/// Só o ID do PC ativo: mudar dados do PC (ex.: último host) não deve derrubar a conexão.

final class ActivePcIdProvider extends $FunctionalProvider<String?, String?, String?> with $Provider<String?> {
  /// Só o ID do PC ativo: mudar dados do PC (ex.: último host) não deve derrubar a conexão.
  ActivePcIdProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'activePcIdProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$activePcIdHash();

  @$internal
  @override
  $ProviderElement<String?> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  String? create(Ref ref) {
    return activePcId(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(String? value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<String?>(value));
  }
}

String _$activePcIdHash() => r'b07dbf840b2b441eac070b16bc8c10a1baeba1f7';
