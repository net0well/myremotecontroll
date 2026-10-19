// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'security_controllers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning
/// Repositório da sessão online; nulo sem conexão.

@ProviderFor(securityRepository)
final securityRepositoryProvider = SecurityRepositoryProvider._();

/// Repositório da sessão online; nulo sem conexão.

final class SecurityRepositoryProvider
    extends $FunctionalProvider<SecurityRepository?, SecurityRepository?, SecurityRepository?>
    with $Provider<SecurityRepository?> {
  /// Repositório da sessão online; nulo sem conexão.
  SecurityRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'securityRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$securityRepositoryHash();

  @$internal
  @override
  $ProviderElement<SecurityRepository?> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  SecurityRepository? create(Ref ref) {
    return securityRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(SecurityRepository? value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<SecurityRepository?>(value));
  }
}

String _$securityRepositoryHash() => r'5f684463233707357dd50e98443495e88205a8aa';

/// Permissões do aparelho no PC ativo, para esconder o que ele não pode fazer (spec 004, AC-7).

@ProviderFor(currentPermissions)
final currentPermissionsProvider = CurrentPermissionsProvider._();

/// Permissões do aparelho no PC ativo, para esconder o que ele não pode fazer (spec 004, AC-7).

final class CurrentPermissionsProvider extends $FunctionalProvider<Set<String>, Set<String>, Set<String>>
    with $Provider<Set<String>> {
  /// Permissões do aparelho no PC ativo, para esconder o que ele não pode fazer (spec 004, AC-7).
  CurrentPermissionsProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'currentPermissionsProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$currentPermissionsHash();

  @$internal
  @override
  $ProviderElement<Set<String>> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  Set<String> create(Ref ref) {
    return currentPermissions(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(Set<String> value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<Set<String>>(value));
  }
}

String _$currentPermissionsHash() => r'0bdbb4995540b04865ec9d6acacefed4a60d9b43';

@ProviderFor(DevicesController)
final devicesControllerProvider = DevicesControllerProvider._();

final class DevicesControllerProvider extends $AsyncNotifierProvider<DevicesController, List<DeviceInfo>> {
  DevicesControllerProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'devicesControllerProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$devicesControllerHash();

  @$internal
  @override
  DevicesController create() => DevicesController();
}

String _$devicesControllerHash() => r'64ed190cdcc0c48fac83003753c5655991a622c0';

abstract class _$DevicesController extends $AsyncNotifier<List<DeviceInfo>> {
  FutureOr<List<DeviceInfo>> build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<AsyncValue<List<DeviceInfo>>, List<DeviceInfo>>;
    final element =
        ref.element
            as $ClassProviderElement<
              AnyNotifier<AsyncValue<List<DeviceInfo>>, List<DeviceInfo>>,
              AsyncValue<List<DeviceInfo>>,
              Object?,
              Object?
            >;
    return element.handleCreate(ref, build);
  }
}

@ProviderFor(AuditController)
final auditControllerProvider = AuditControllerProvider._();

final class AuditControllerProvider extends $AsyncNotifierProvider<AuditController, AuditState> {
  AuditControllerProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'auditControllerProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$auditControllerHash();

  @$internal
  @override
  AuditController create() => AuditController();
}

String _$auditControllerHash() => r'f86e06105fea58cd6412f22be41da06a8a4deffc';

abstract class _$AuditController extends $AsyncNotifier<AuditState> {
  FutureOr<AuditState> build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<AsyncValue<AuditState>, AuditState>;
    final element =
        ref.element
            as $ClassProviderElement<
              AnyNotifier<AsyncValue<AuditState>, AuditState>,
              AsyncValue<AuditState>,
              Object?,
              Object?
            >;
    return element.handleCreate(ref, build);
  }
}
