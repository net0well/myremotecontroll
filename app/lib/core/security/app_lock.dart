import 'package:local_auth/local_auth.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/settings/logic/app_settings.dart';
import '../providers.dart';

part 'app_lock.g.dart';

/// Resultado de pedir biometria/PIN ao aparelho.
enum DeviceAuthResult {
  success,

  /// Recusada, cancelada ou bloqueada por tentativas: a ação não segue.
  failed,

  /// O aparelho não tem biometria nem PIN configurados: não há como autenticar (a spec 004 libera nesse caso).
  unavailable,
}

/// Biometria ou PIN do aparelho. A implementação real usa o `local_auth`; os testes usam uma falsa.
abstract interface class DeviceAuthenticator {
  Future<DeviceAuthResult> authenticate(String reason);
}

class LocalAuthAuthenticator implements DeviceAuthenticator {
  LocalAuthAuthenticator([LocalAuthentication? localAuth]) : _localAuth = localAuth ?? LocalAuthentication();

  final LocalAuthentication _localAuth;

  /// Aceita biometria ou o PIN/padrão do aparelho (não só biometria), como pede a spec 004. O `local_auth` 3 lança
  /// exceção quando não há credencial e quando o usuário cancela; aqui isso vira um resultado.
  @override
  Future<DeviceAuthResult> authenticate(String reason) async {
    if (!await _localAuth.isDeviceSupported()) {
      return DeviceAuthResult.unavailable;
    }

    try {
      final ok = await _localAuth.authenticate(localizedReason: reason, persistAcrossBackgrounding: true);
      return ok ? DeviceAuthResult.success : DeviceAuthResult.failed;
    } on LocalAuthException catch (error) {
      return switch (error.code) {
        LocalAuthExceptionCode.noCredentialsSet ||
        LocalAuthExceptionCode.noBiometricsEnrolled ||
        LocalAuthExceptionCode.noBiometricHardware => DeviceAuthResult.unavailable,
        _ => DeviceAuthResult.failed,
      };
    }
  }
}

@Riverpod(keepAlive: true)
DeviceAuthenticator deviceAuthenticator(Ref ref) => LocalAuthAuthenticator();

enum AppLockStatus { unlocked, locked }

/// Bloqueio do app (spec 004, AC-3): bloqueado ao abrir e após o tempo configurado em segundo plano, se a biometria
/// estiver ligada e o aparelho tiver biometria ou PIN.
@Riverpod(keepAlive: true)
class AppLock extends _$AppLock {
  DateTime? _backgroundedAt;

  @override
  AppLockStatus build() =>
      ref.read(appSettingsControllerProvider).biometricLock ? AppLockStatus.locked : AppLockStatus.unlocked;

  /// Guarda só a primeira entrada em segundo plano: na volta o sistema passa de novo por `hidden`
  /// (paused → hidden → inactive → resumed), e regravar ali zeraria o tempo e o bloqueio nunca aconteceria.
  void onBackgrounded() => _backgroundedAt ??= ref.read(clockProvider)();

  void onForegrounded() {
    final since = _backgroundedAt;
    _backgroundedAt = null;
    final settings = ref.read(appSettingsControllerProvider);
    if (since != null &&
        settings.biometricLock &&
        ref.read(clockProvider)().difference(since) >= settings.lockTimeout) {
      state = AppLockStatus.locked;
    }
  }

  /// Pede biometria/PIN. Sem nenhum dos dois configurado no aparelho, o bloqueio não tem como funcionar e é liberado.
  Future<bool> unlock(String reason) async {
    final result = await ref.read(deviceAuthenticatorProvider).authenticate(reason);
    if (result == DeviceAuthResult.failed) {
      return false;
    }

    if (ref.mounted) {
      state = AppLockStatus.unlocked;
    }

    return true;
  }
}
