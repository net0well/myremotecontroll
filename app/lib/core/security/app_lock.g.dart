// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app_lock.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(deviceAuthenticator)
final deviceAuthenticatorProvider = DeviceAuthenticatorProvider._();

final class DeviceAuthenticatorProvider
    extends $FunctionalProvider<DeviceAuthenticator, DeviceAuthenticator, DeviceAuthenticator>
    with $Provider<DeviceAuthenticator> {
  DeviceAuthenticatorProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'deviceAuthenticatorProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$deviceAuthenticatorHash();

  @$internal
  @override
  $ProviderElement<DeviceAuthenticator> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  DeviceAuthenticator create(Ref ref) {
    return deviceAuthenticator(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(DeviceAuthenticator value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<DeviceAuthenticator>(value));
  }
}

String _$deviceAuthenticatorHash() => r'5523e76d07f9188f22429481e3ef6da89d6a4330';

/// Bloqueio do app (spec 004, AC-3): bloqueado ao abrir e após o tempo configurado em segundo plano, se a biometria
/// estiver ligada e o aparelho tiver biometria ou PIN.

@ProviderFor(AppLock)
final appLockProvider = AppLockProvider._();

/// Bloqueio do app (spec 004, AC-3): bloqueado ao abrir e após o tempo configurado em segundo plano, se a biometria
/// estiver ligada e o aparelho tiver biometria ou PIN.
final class AppLockProvider extends $NotifierProvider<AppLock, AppLockStatus> {
  /// Bloqueio do app (spec 004, AC-3): bloqueado ao abrir e após o tempo configurado em segundo plano, se a biometria
  /// estiver ligada e o aparelho tiver biometria ou PIN.
  AppLockProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'appLockProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$appLockHash();

  @$internal
  @override
  AppLock create() => AppLock();

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(AppLockStatus value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<AppLockStatus>(value));
  }
}

String _$appLockHash() => r'6e8981230e16af1f7f47633e3aedef7224ccc564';

/// Bloqueio do app (spec 004, AC-3): bloqueado ao abrir e após o tempo configurado em segundo plano, se a biometria
/// estiver ligada e o aparelho tiver biometria ou PIN.

abstract class _$AppLock extends $Notifier<AppLockStatus> {
  AppLockStatus build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<AppLockStatus, AppLockStatus>;
    final element =
        ref.element
            as $ClassProviderElement<AnyNotifier<AppLockStatus, AppLockStatus>, AppLockStatus, Object?, Object?>;
    return element.handleCreate(ref, build);
  }
}
