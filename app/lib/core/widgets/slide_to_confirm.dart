import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';

/// "Deslize para confirmar" das ações destrutivas (spec 005): exige um gesto deliberado, não um toque acidental.
class SlideToConfirm extends StatefulWidget {
  const SlideToConfirm({super.key, required this.label, required this.onConfirmed, this.color});

  final String label;
  final VoidCallback onConfirmed;
  final Color? color;

  @override
  State<SlideToConfirm> createState() => _SlideToConfirmState();
}

class _SlideToConfirmState extends State<SlideToConfirm> {
  static const _height = 56.0;
  static const _threshold = 0.9;

  double _progress = 0;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = widget.color ?? scheme.primary;

    return Semantics(
      button: true,
      label: widget.label,
      onTap: widget.onConfirmed,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final travel = constraints.maxWidth - _height;
          return GestureDetector(
            onHorizontalDragUpdate: (details) =>
                setState(() => _progress = (_progress + details.delta.dx / travel).clamp(0.0, 1.0)),
            onHorizontalDragEnd: (_) {
              if (_progress >= _threshold) {
                HapticFeedback.mediumImpact();
                widget.onConfirmed();
              }
              setState(() => _progress = 0);
            },
            child: Container(
              height: _height,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(_height / 2),
              ),
              child: Stack(
                alignment: Alignment.centerLeft,
                children: [
                  Center(
                    child: ExcludeSemantics(
                      child: Text(widget.label, style: Theme.of(context).textTheme.labelLarge?.copyWith(color: color)),
                    ),
                  ),
                  AnimatedPositioned(
                    duration: _progress == 0 ? context.motion(AppDurations.fast) : Duration.zero,
                    left: _progress * travel,
                    child: Container(
                      width: _height,
                      height: _height,
                      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                      child: Icon(Icons.chevron_right_rounded, color: scheme.onPrimary, size: 32),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
