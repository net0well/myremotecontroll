import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/app_localizations.dart';
import 'live_indicators.dart';
import 'status_badge.dart';

/// Estados visuais de um card do painel (seção 4.3): cada um tem preview de widget.
enum DashCardState { ready, loading, noPermission, offline }

/// Card do painel: título com ícone, valor principal grande, detalhe secundário. Toque abre a tela detalhada.
class DashCard extends StatelessWidget {
  const DashCard({
    super.key,
    required this.title,
    required this.icon,
    this.child,
    this.state = DashCardState.ready,
    this.severity,
    this.severityLabel,
    this.trailing,
    this.onTap,
  });

  final String title;
  final IconData icon;
  final Widget? child;
  final DashCardState state;

  /// Destaque por limiar (atenção, crítico) com selo de texto; nulo no estado normal.
  final Severity? severity;
  final String? severityLabel;
  final Widget? trailing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context);
    final highlight = severity == null || severity == Severity.normal ? null : severity!.color;

    return Card(
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadii.card),
        side: highlight == null ? BorderSide.none : BorderSide(color: highlight, width: 1.5),
      ),
      child: InkWell(
        onTap: state == DashCardState.noPermission ? null : onTap,
        child: AnimatedOpacity(
          opacity: state == DashCardState.offline ? 0.55 : 1,
          duration: context.motion(AppDurations.fast),
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(icon, size: 20, color: theme.colorScheme.primary),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Text(title, style: theme.textTheme.titleMedium, overflow: TextOverflow.ellipsis),
                    ),
                    ?trailing,
                  ],
                ),
                const SizedBox(height: AppSpacing.md),
                Expanded(
                  child: switch (state) {
                    DashCardState.loading => const Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Skeleton(width: 96, height: 28),
                        SizedBox(height: AppSpacing.sm),
                        Skeleton(width: 140),
                      ],
                    ),
                    DashCardState.noPermission => Row(
                      children: [
                        Icon(Icons.lock_outline_rounded, size: 18, color: theme.colorScheme.onSurfaceVariant),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(child: Text(l10n.errorForbidden, style: theme.textTheme.bodySmall)),
                      ],
                    ),
                    _ => child ?? const SizedBox.shrink(),
                  },
                ),
                if (severity != null && severityLabel != null) ...[
                  const SizedBox(height: AppSpacing.sm),
                  StatusBadge(severity: severity!, label: severityLabel!),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
