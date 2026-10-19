import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../core/storage/paired_pcs.dart';
import '../features/dashboard/ui/card_detail_screen.dart';
import '../features/dashboard/ui/dashboard_screen.dart';
import '../features/pairing/ui/pairing_screen.dart';
import '../features/security/ui/security_screens.dart';
import '../features/settings/ui/settings_screen.dart';

part 'router.g.dart';

/// Rotas do app. Sem PC pareado, tudo leva ao pareamento.
@Riverpod(keepAlive: true)
GoRouter router(Ref ref) {
  final refresh = ValueNotifier(0);
  ref.listen(pairedPcsProvider.select((s) => s.pcs.isEmpty), (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);

  final router = GoRouter(
    initialLocation: '/',
    refreshListenable: refresh,
    redirect: (context, state) {
      final noPcs = ref.read(pairedPcsProvider).pcs.isEmpty;
      return noPcs && state.matchedLocation != '/pairing' ? '/pairing' : null;
    },
    routes: [
      GoRoute(path: '/', builder: (_, _) => const DashboardScreen()),
      GoRoute(
        path: '/card/:id',
        builder: (_, state) => CardDetailScreen(cardId: state.pathParameters['id']!),
      ),
      GoRoute(path: '/pairing', builder: (_, _) => const PairingScreen()),
      GoRoute(
        path: '/settings',
        builder: (_, _) => const SettingsScreen(),
        routes: [
          GoRoute(path: 'devices', builder: (_, _) => const DevicesScreen()),
          GoRoute(path: 'audit', builder: (_, _) => const AuditScreen()),
        ],
      ),
    ],
  );
  ref.onDispose(router.dispose);
  return router;
}
