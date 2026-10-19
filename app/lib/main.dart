import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app/app.dart';
import 'core/providers.dart';
import 'core/storage/stores.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final preferences = await SharedPreferencesWithCache.create(cacheOptions: const SharedPreferencesWithCacheOptions());

  runApp(
    ProviderScope(
      overrides: [keyValueStoreProvider.overrideWithValue(SharedPreferencesStore(preferences))],
      child: const MyRemoteControllApp(),
    ),
  );
}
