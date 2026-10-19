import 'dart:convert';

import 'package:material_ui/material_ui.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/providers.dart';

part 'app_settings.g.dart';

/// Configurações locais do app (spec 004): tema escuro por padrão, biometria ligada por padrão, bloqueio após 5 min.
class AppSettings {
  const AppSettings({
    this.themeMode = ThemeMode.dark,
    this.dynamicColor = false,
    this.biometricLock = true,
    this.lockTimeoutMinutes = 5,
  });

  factory AppSettings.fromJson(Map<String, dynamic> json) => AppSettings(
    themeMode: ThemeMode.values.firstWhere((m) => m.name == json['themeMode'], orElse: () => ThemeMode.dark),
    dynamicColor: json['dynamicColor'] as bool? ?? false,
    biometricLock: json['biometricLock'] as bool? ?? true,
    lockTimeoutMinutes: json['lockTimeoutMinutes'] as int? ?? 5,
  );

  static const lockTimeoutOptions = [1, 5, 15, 30];

  final ThemeMode themeMode;
  final bool dynamicColor;
  final bool biometricLock;
  final int lockTimeoutMinutes;

  Duration get lockTimeout => Duration(minutes: lockTimeoutMinutes);

  AppSettings copyWith({ThemeMode? themeMode, bool? dynamicColor, bool? biometricLock, int? lockTimeoutMinutes}) =>
      AppSettings(
        themeMode: themeMode ?? this.themeMode,
        dynamicColor: dynamicColor ?? this.dynamicColor,
        biometricLock: biometricLock ?? this.biometricLock,
        lockTimeoutMinutes: lockTimeoutMinutes ?? this.lockTimeoutMinutes,
      );

  Map<String, dynamic> toJson() => {
    'themeMode': themeMode.name,
    'dynamicColor': dynamicColor,
    'biometricLock': biometricLock,
    'lockTimeoutMinutes': lockTimeoutMinutes,
  };
}

@Riverpod(keepAlive: true)
class AppSettingsController extends _$AppSettingsController {
  static const _key = 'app_settings.v1';

  @override
  AppSettings build() {
    final raw = ref.watch(keyValueStoreProvider).getString(_key);
    return raw == null ? const AppSettings() : AppSettings.fromJson(jsonDecode(raw) as Map<String, dynamic>);
  }

  Future<void> change(AppSettings Function(AppSettings current) update) async {
    final next = update(state);
    await ref.read(keyValueStoreProvider).setString(_key, jsonEncode(next.toJson()));
    state = next;
  }
}
