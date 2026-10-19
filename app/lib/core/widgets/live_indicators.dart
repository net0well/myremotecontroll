import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/app_localizations.dart';
import '../formatting/durations.dart';

/// Ponto pulsante "ao vivo"; parado quando o sistema pede para reduzir movimento.
class LiveIndicator extends StatefulWidget {
  const LiveIndicator({super.key});

  @override
  State<LiveIndicator> createState() => _LiveIndicatorState();
}

class _LiveIndicatorState extends State<LiveIndicator> with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(vsync: this, duration: AppDurations.pulse);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (context.reduceMotion) {
      _pulse
        ..stop()
        ..value = 1;
    } else if (!_pulse.isAnimating) {
      _pulse.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final label = AppLocalizations.of(context).liveLabel;
    return Semantics(
      label: label,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          FadeTransition(
            opacity: Tween<double>(begin: 0.35, end: 1).animate(_pulse),
            child: const DecoratedBox(
              decoration: BoxDecoration(color: AppColors.normal, shape: BoxShape.circle),
              child: SizedBox.square(dimension: 8),
            ),
          ),
          const SizedBox(width: AppSpacing.xs),
          ExcludeSemantics(child: Text(label, style: Theme.of(context).textTheme.labelSmall)),
        ],
      ),
    );
  }
}

/// "há 12 s": quanto tempo faz desde o último dado recebido.
class StaleBadge extends StatelessWidget {
  const StaleBadge({super.key, required this.age});

  final Duration age;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.history_rounded, size: 14, color: scheme.onSurfaceVariant),
        const SizedBox(width: AppSpacing.xs),
        Text(
          l10n.staleSince(formatDuration(l10n, age)),
          style: Theme.of(context).textTheme.labelSmall?.copyWith(color: scheme.onSurfaceVariant),
        ),
      ],
    );
  }
}

/// Bloco cinza no lugar do conteúdo enquanto carrega.
class Skeleton extends StatelessWidget {
  const Skeleton({super.key, this.width, this.height = 16});

  final double? width;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.onSurface.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppRadii.chip / 2),
      ),
    );
  }
}
