import 'package:material_ui/material_ui.dart';

/// Tokens do design system (documento base, seção 4.3). Definidos uma única vez; telas e componentes só os usam.
abstract final class AppColors {
  /// Azul elétrico, cor de destaque da marca (spec 004).
  static const brand = Color(0xFF3D7BFF);

  /// Cores semânticas por limiar, sempre acompanhadas de ícone ou texto.
  static const normal = Color(0xFF22C55E);
  static const attention = Color(0xFFF59E0B);
  static const critical = Color(0xFFEF4444);
  static const offline = Color(0xFF6B7280);
}

abstract final class AppSpacing {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 24.0;
  static const xxl = 32.0;
}

abstract final class AppRadii {
  static const card = 20.0;
  static const chip = 12.0;
  static const button = 14.0;
}

abstract final class AppDurations {
  /// Transição de valores ao vivo (200–300 ms).
  static const value = Duration(milliseconds: 260);
  static const fast = Duration(milliseconds: 150);
  static const pulse = Duration(milliseconds: 1400);
}

/// Larguras que mudam a grade do painel: 1–2 colunas no celular, 3–4 em tablet/paisagem.
abstract final class AppBreakpoints {
  static int columnsFor(double width) => switch (width) {
    < 420 => 1,
    < 720 => 2,
    < 1080 => 3,
    _ => 4,
  };
}

/// Respeita "reduzir movimento" do sistema: animações viram mudanças instantâneas (spec 004, AC-6).
extension MotionPreference on BuildContext {
  bool get reduceMotion => MediaQuery.disableAnimationsOf(this);

  Duration motion(Duration duration) => reduceMotion ? Duration.zero : duration;
}
