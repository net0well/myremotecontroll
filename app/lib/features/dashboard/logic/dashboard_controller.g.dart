// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dashboard_controller.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(dataSaver)
final dataSaverProvider = DataSaverProvider._();

final class DataSaverProvider extends $FunctionalProvider<DataSaver, DataSaver, DataSaver> with $Provider<DataSaver> {
  DataSaverProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'dataSaverProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$dataSaverHash();

  @$internal
  @override
  $ProviderElement<DataSaver> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  DataSaver create(Ref ref) {
    return dataSaver(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(DataSaver value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<DataSaver>(value));
  }
}

String _$dataSaverHash() => r'ae61efa1abd931c27799463dbbaf305fbc363bb5';

/// Painel do PC ativo (ADR-0021): assina ao conectar, aplica os deltas em ordem e pede snapshot quando a versão pula
/// (AC-3). Cada card muda só quando o seu pedaço muda, então só ele reconstrói (AC-5).

@ProviderFor(DashboardController)
final dashboardControllerProvider = DashboardControllerProvider._();

/// Painel do PC ativo (ADR-0021): assina ao conectar, aplica os deltas em ordem e pede snapshot quando a versão pula
/// (AC-3). Cada card muda só quando o seu pedaço muda, então só ele reconstrói (AC-5).
final class DashboardControllerProvider extends $NotifierProvider<DashboardController, DashboardState> {
  /// Painel do PC ativo (ADR-0021): assina ao conectar, aplica os deltas em ordem e pede snapshot quando a versão pula
  /// (AC-3). Cada card muda só quando o seu pedaço muda, então só ele reconstrói (AC-5).
  DashboardControllerProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'dashboardControllerProvider',
        isAutoDispose: false,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$dashboardControllerHash();

  @$internal
  @override
  DashboardController create() => DashboardController();

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(DashboardState value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<DashboardState>(value));
  }
}

String _$dashboardControllerHash() => r'053a29bf8c243760d9b967e1d0a5ad35cc1ed400';

/// Painel do PC ativo (ADR-0021): assina ao conectar, aplica os deltas em ordem e pede snapshot quando a versão pula
/// (AC-3). Cada card muda só quando o seu pedaço muda, então só ele reconstrói (AC-5).

abstract class _$DashboardController extends $Notifier<DashboardState> {
  DashboardState build();
  @$mustCallSuper
  @override
  WhenComplete runBuild() {
    final ref = this.ref as $Ref<DashboardState, DashboardState>;
    final element =
        ref.element
            as $ClassProviderElement<AnyNotifier<DashboardState, DashboardState>, DashboardState, Object?, Object?>;
    return element.handleCreate(ref, build);
  }
}

/// Um card do painel; só notifica quando esse card muda (AC-5).

@ProviderFor(dashboardCard)
final dashboardCardProvider = DashboardCardFamily._();

/// Um card do painel; só notifica quando esse card muda (AC-5).

final class DashboardCardProvider extends $FunctionalProvider<CardState?, CardState?, CardState?>
    with $Provider<CardState?> {
  /// Um card do painel; só notifica quando esse card muda (AC-5).
  DashboardCardProvider._({required DashboardCardFamily super.from, required String super.argument})
    : super(
        retry: null,
        name: r'dashboardCardProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$dashboardCardHash();

  @override
  String toString() {
    return r'dashboardCardProvider'
        ''
        '($argument)';
  }

  @$internal
  @override
  $ProviderElement<CardState?> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  CardState? create(Ref ref) {
    final argument = this.argument as String;
    return dashboardCard(ref, argument);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(CardState? value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<CardState?>(value));
  }

  @override
  bool operator ==(Object other) {
    return other is DashboardCardProvider && other.argument == argument;
  }

  @override
  int get hashCode {
    return argument.hashCode;
  }
}

String _$dashboardCardHash() => r'7213936e4456c6f8d4c7f652701a756ea8dac285';

/// Um card do painel; só notifica quando esse card muda (AC-5).

final class DashboardCardFamily extends $Family with $FunctionalFamilyOverride<CardState?, String> {
  DashboardCardFamily._()
    : super(
        retry: null,
        name: r'dashboardCardProvider',
        dependencies: null,
        $allTransitiveDependencies: null,
        isAutoDispose: true,
      );

  /// Um card do painel; só notifica quando esse card muda (AC-5).

  DashboardCardProvider call(String cardId) => DashboardCardProvider._(argument: cardId, from: this);

  @override
  String toString() => r'dashboardCardProvider';
}

/// Série de uma sparkline (últimos 60 valores).

@ProviderFor(dashboardHistory)
final dashboardHistoryProvider = DashboardHistoryFamily._();

/// Série de uma sparkline (últimos 60 valores).

final class DashboardHistoryProvider extends $FunctionalProvider<List<double>, List<double>, List<double>>
    with $Provider<List<double>> {
  /// Série de uma sparkline (últimos 60 valores).
  DashboardHistoryProvider._({required DashboardHistoryFamily super.from, required String super.argument})
    : super(
        retry: null,
        name: r'dashboardHistoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$dashboardHistoryHash();

  @override
  String toString() {
    return r'dashboardHistoryProvider'
        ''
        '($argument)';
  }

  @$internal
  @override
  $ProviderElement<List<double>> $createElement($ProviderPointer pointer) => $ProviderElement(pointer);

  @override
  List<double> create(Ref ref) {
    final argument = this.argument as String;
    return dashboardHistory(ref, argument);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(List<double> value) {
    return $ProviderOverride(origin: this, providerOverride: $SyncValueProvider<List<double>>(value));
  }

  @override
  bool operator ==(Object other) {
    return other is DashboardHistoryProvider && other.argument == argument;
  }

  @override
  int get hashCode {
    return argument.hashCode;
  }
}

String _$dashboardHistoryHash() => r'1ae5e987696deaebcc04cd1f3a1f80a9c9ab77ce';

/// Série de uma sparkline (últimos 60 valores).

final class DashboardHistoryFamily extends $Family with $FunctionalFamilyOverride<List<double>, String> {
  DashboardHistoryFamily._()
    : super(
        retry: null,
        name: r'dashboardHistoryProvider',
        dependencies: null,
        $allTransitiveDependencies: null,
        isAutoDispose: true,
      );

  /// Série de uma sparkline (últimos 60 valores).

  DashboardHistoryProvider call(String series) => DashboardHistoryProvider._(argument: series, from: this);

  @override
  String toString() => r'dashboardHistoryProvider';
}
