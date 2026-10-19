import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';

/// Severidade por limiar (seção 4.3): cor sempre acompanhada de ícone e texto, nunca só a cor.
enum Severity {
  normal(AppColors.normal, Icons.check_circle_rounded),
  attention(AppColors.attention, Icons.warning_amber_rounded),
  critical(AppColors.critical, Icons.error_rounded),
  offline(AppColors.offline, Icons.cloud_off_rounded);

  const Severity(this.color, this.icon);

  final Color color;
  final IconData icon;
}

class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.severity, required this.label});

  final Severity severity;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: label,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: AppSpacing.xs),
        decoration: BoxDecoration(
          color: severity.color.withValues(alpha: 0.14),
          borderRadius: BorderRadius.circular(AppRadii.chip),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(severity.icon, size: 16, color: severity.color),
            const SizedBox(width: AppSpacing.xs),
            ExcludeSemantics(
              child: Text(label, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: severity.color)),
            ),
          ],
        ),
      ),
    );
  }
}
