import 'package:dynamic_color/dynamic_color.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:material_ui/material_ui.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../core/security/app_lock.dart';
import '../features/settings/logic/app_settings.dart';
import '../l10n/app_localizations.dart';
import 'router.dart';
import 'theme/app_theme.dart';
import 'theme/tokens.dart';

part 'app.g.dart';

/// Cor-base do Material You (Android 12+). Lê só a paleta crua: os tipos de tema do pacote dynamic_color ainda são os
/// do Material do SDK, incompatíveis com o material_ui (risco R10).
@Riverpod(keepAlive: true)
Future<Color?> dynamicSeedColor(Ref ref) async {
  final palette = await DynamicColorPlugin.getCorePalette();
  return palette == null ? null : Color(palette.primary.get(40));
}

class MyRemoteControllApp extends ConsumerWidget {
  const MyRemoteControllApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(appSettingsControllerProvider);
    final dynamicSeed = settings.dynamicColor ? ref.watch(dynamicSeedColorProvider).value : null;
    final seed = dynamicSeed ?? AppColors.brand;

    return MaterialApp.router(
      title: 'MyRemoteControll',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(seed: seed),
      darkTheme: AppTheme.dark(seed: seed),
      themeMode: settings.themeMode,
      // Idioma fixo em pt-BR, mesmo com o sistema em outro idioma (spec 004, AC-4).
      locale: const Locale('pt'),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: const [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
      routerConfig: ref.watch(routerProvider),
      builder: (context, child) => AppLockGate(child: child ?? const SizedBox.shrink()),
    );
  }
}

/// Cobre o app com a tela de bloqueio ao abrir e após o tempo em segundo plano (spec 004, AC-3). O conteúdo fica por
/// baixo, sem ser mostrado nem receber toques.
class AppLockGate extends ConsumerStatefulWidget {
  const AppLockGate({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<AppLockGate> createState() => _AppLockGateState();
}

class _AppLockGateState extends ConsumerState<AppLockGate> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    final lock = ref.read(appLockProvider.notifier);
    switch (state) {
      case AppLifecycleState.hidden || AppLifecycleState.paused:
        lock.onBackgrounded();
      case AppLifecycleState.resumed:
        lock.onForegrounded();
      case AppLifecycleState.inactive || AppLifecycleState.detached:
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    final locked = ref.watch(appLockProvider) == AppLockStatus.locked;
    return Stack(
      children: [
        ExcludeSemantics(
          excluding: locked,
          child: TickerMode(enabled: !locked, child: widget.child),
        ),
        if (locked) const Positioned.fill(child: LockScreen()),
      ],
    );
  }
}

class LockScreen extends ConsumerWidget {
  const LockScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    return Material(
      color: theme.colorScheme.surface,
      child: SafeArea(
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.lock_rounded, size: 64, color: theme.colorScheme.primary),
              const SizedBox(height: AppSpacing.lg),
              Text(l10n.lockTitle, style: theme.textTheme.headlineSmall),
              const SizedBox(height: AppSpacing.xl),
              FilledButton.icon(
                key: const Key('unlock'),
                onPressed: () => ref.read(appLockProvider.notifier).unlock(l10n.lockReason),
                icon: const Icon(Icons.fingerprint_rounded),
                label: Text(l10n.lockUnlock),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
