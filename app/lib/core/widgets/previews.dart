import 'package:flutter/widget_previews.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme/app_theme.dart';
import '../../l10n/app_localizations.dart';
import 'charts.dart';
import 'dash_card.dart';
import 'live_indicators.dart';
import 'slide_to_confirm.dart';
import 'status_badge.dart';

// Previews dos componentes do design system (seção 4.3), nos estados normal, atenção, crítico, carregando, sem
// permissão e offline. Abra com `flutter widget-preview start` ou pela aba "Flutter Widget Preview" da IDE.
//
// O tema entra pelo wrapper (e não pelo parâmetro `theme`) porque o app usa o pacote material_ui, cujo ThemeData é
// outro tipo em relação ao do Material embutido no SDK.

Widget _app(Widget child, ThemeData theme) => MaterialApp(
  theme: theme,
  debugShowCheckedModeBanner: false,
  locale: const Locale('pt'),
  // Os mesmos delegates do app: os gerados apontam para o Material do SDK, e não para o do material_ui.
  localizationsDelegates: const [AppLocalizations.delegate, ...GlobalMaterialLocalizations.delegates],
  supportedLocales: AppLocalizations.supportedLocales,
  home: Scaffold(
    body: Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: SizedBox(width: 320, height: 200, child: child),
      ),
    ),
  ),
);

Widget darkPreviewWrapper(Widget child) => _app(child, AppTheme.dark());

Widget lightPreviewWrapper(Widget child) => _app(child, AppTheme.light());

Widget _cpuContent() => const Row(
  children: [
    Gauge(value: 0.42, label: 'CPU', size: 96),
    SizedBox(width: 12),
    Expanded(child: Sparkline(values: [12, 30, 22, 45, 38, 52, 41, 47, 42])),
  ],
);

@Preview(group: 'DashCard', name: 'Normal (escuro)', wrapper: darkPreviewWrapper)
Widget dashCardNormal() =>
    DashCard(title: 'CPU', icon: Icons.memory_rounded, trailing: const LiveIndicator(), child: _cpuContent());

@Preview(group: 'DashCard', name: 'Normal (claro)', wrapper: lightPreviewWrapper)
Widget dashCardNormalLight() => DashCard(title: 'CPU', icon: Icons.memory_rounded, child: _cpuContent());

@Preview(group: 'DashCard', name: 'Atenção', wrapper: darkPreviewWrapper)
Widget dashCardAttention() => const DashCard(
  title: 'Temperatura',
  icon: Icons.thermostat_rounded,
  severity: Severity.attention,
  severityLabel: '78 °C — atenção',
  child: Gauge(value: 0.78, label: 'Temperatura', severity: Severity.attention, size: 96),
);

@Preview(group: 'DashCard', name: 'Crítico', wrapper: darkPreviewWrapper)
Widget dashCardCritical() => const DashCard(
  title: 'Disco C:',
  icon: Icons.storage_rounded,
  severity: Severity.critical,
  severityLabel: '3% livre — crítico',
  child: Gauge(value: 0.97, label: 'Disco', severity: Severity.critical, size: 96),
);

@Preview(group: 'DashCard', name: 'Carregando', wrapper: darkPreviewWrapper)
Widget dashCardLoading() =>
    const DashCard(title: 'Memória', icon: Icons.developer_board_rounded, state: DashCardState.loading);

@Preview(group: 'DashCard', name: 'Sem permissão', wrapper: darkPreviewWrapper)
Widget dashCardNoPermission() =>
    const DashCard(title: 'Arquivos', icon: Icons.folder_rounded, state: DashCardState.noPermission);

@Preview(group: 'DashCard', name: 'Offline', wrapper: darkPreviewWrapper)
Widget dashCardOffline() => DashCard(
  title: 'CPU',
  icon: Icons.memory_rounded,
  state: DashCardState.offline,
  trailing: const StaleBadge(age: Duration(seconds: 42)),
  child: _cpuContent(),
);

@Preview(group: 'Componentes', name: 'Selos de estado', wrapper: darkPreviewWrapper)
Widget statusBadges() => const Wrap(
  spacing: 8,
  runSpacing: 8,
  children: [
    StatusBadge(severity: Severity.normal, label: 'Normal'),
    StatusBadge(severity: Severity.attention, label: 'Atenção'),
    StatusBadge(severity: Severity.critical, label: 'Crítico'),
    StatusBadge(severity: Severity.offline, label: 'Offline'),
  ],
);

@Preview(group: 'Componentes', name: 'Deslizar para confirmar', wrapper: darkPreviewWrapper)
Widget slideToConfirm() => SlideToConfirm(label: 'Deslize para desligar', onConfirmed: () {});
