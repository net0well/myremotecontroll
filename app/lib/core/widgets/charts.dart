import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import 'status_badge.dart';

/// Gauge circular com o valor animado até o novo número (spec 007, AC-1); instantâneo com "reduzir movimento".
class Gauge extends StatelessWidget {
  const Gauge({super.key, required this.value, required this.label, this.severity = Severity.normal, this.size = 112});

  /// De 0 a 1.
  final double value;
  final String label;
  final Severity severity;
  final double size;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final clamped = value.clamp(0.0, 1.0);
    return Semantics(
      label: '$label ${(clamped * 100).round()}%',
      child: TweenAnimationBuilder<double>(
        tween: Tween(end: clamped),
        duration: context.motion(AppDurations.value),
        curve: Curves.easeOutCubic,
        builder: (context, animated, _) => SizedBox.square(
          dimension: size,
          child: CustomPaint(
            painter: _GaugePainter(animated, severity.color, scheme.onSurface.withValues(alpha: 0.10)),
            child: Center(
              child: ExcludeSemantics(
                child: Text(
                  '${(animated * 100).round()}%',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _GaugePainter extends CustomPainter {
  _GaugePainter(this.value, this.color, this.track);

  final double value;
  final Color color;
  final Color track;

  static const _start = 0.75 * pi;
  static const _sweep = 1.5 * pi;

  @override
  void paint(Canvas canvas, Size size) {
    final stroke = size.width * 0.09;
    final rect = Offset.zero & size;
    final arcRect = rect.deflate(stroke / 2);
    final base = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;

    canvas
      ..drawArc(arcRect, _start, _sweep, false, base..color = track)
      ..drawArc(arcRect, _start, _sweep * value, false, base..color = color);
  }

  @override
  bool shouldRepaint(_GaugePainter old) => old.value != value || old.color != color || old.track != track;
}

/// Linha dos últimos valores (ex.: 60 s de CPU).
class Sparkline extends StatelessWidget {
  const Sparkline({super.key, required this.values, this.color, this.height = 36, this.maxValue});

  final List<double> values;
  final Color? color;
  final double height;

  /// Teto do eixo; nulo usa o maior valor da série.
  final double? maxValue;

  @override
  Widget build(BuildContext context) {
    return ExcludeSemantics(
      child: SizedBox(
        height: height,
        width: double.infinity,
        child: CustomPaint(
          painter: _SparklinePainter(values, color ?? Theme.of(context).colorScheme.primary, maxValue),
        ),
      ),
    );
  }
}

class _SparklinePainter extends CustomPainter {
  _SparklinePainter(this.values, this.color, this.maxValue);

  final List<double> values;
  final Color color;
  final double? maxValue;

  @override
  void paint(Canvas canvas, Size size) {
    if (values.length < 2) {
      return;
    }

    final top = max(maxValue ?? values.reduce(max), 1e-9);
    final dx = size.width / (values.length - 1);
    final path = Path();
    for (var i = 0; i < values.length; i++) {
      final point = Offset(i * dx, size.height - (values[i].clamp(0, top) / top) * size.height);
      i == 0 ? path.moveTo(point.dx, point.dy) : path.lineTo(point.dx, point.dy);
    }

    final fill = Path.from(path)
      ..lineTo(size.width, size.height)
      ..lineTo(0, size.height)
      ..close();
    canvas
      ..drawPath(fill, Paint()..color = color.withValues(alpha: 0.15))
      ..drawPath(
        path,
        Paint()
          ..color = color
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeJoin = StrokeJoin.round,
      );
  }

  @override
  bool shouldRepaint(_SparklinePainter old) => old.values != values || old.color != color || old.maxValue != maxValue;
}
