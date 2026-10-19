import 'package:intl/intl.dart';

/// Números do painel em pt-BR (vírgula decimal): bytes, taxas, percentuais, clock e contagem regressiva.
abstract final class Units {
  static final _oneDecimal = NumberFormat('0.0', 'pt');
  static final _integer = NumberFormat('0', 'pt');

  static const _byteUnits = ['B', 'KB', 'MB', 'GB', 'TB'];

  /// "11,0 GB", "512 MB", "0 B" (base 1024, como o Windows).
  static String bytes(int? value) {
    if (value == null) {
      return '—';
    }

    var size = value.toDouble();
    var unit = 0;
    while (size >= 1024 && unit < _byteUnits.length - 1) {
      size /= 1024;
      unit++;
    }

    final number = unit == 0 || size >= 100 ? _integer.format(size) : _oneDecimal.format(size);
    return '$number ${_byteUnits[unit]}';
  }

  static String rate(int? bytesPerSecond) => bytesPerSecond == null ? '—' : '${bytes(bytesPerSecond)}/s';

  static String percent(double? value) => value == null ? '—' : '${_integer.format(value)}%';

  static String clock(double? mhz) => mhz == null
      ? '—'
      : mhz >= 1000
      ? '${_oneDecimal.format(mhz / 1000)} GHz'
      : '${_integer.format(mhz)} MHz';

  /// "4:58" ou "1:02:03".
  static String countdown(Duration remaining) {
    final value = remaining.isNegative ? Duration.zero : remaining;
    final hours = value.inHours;
    final minutes = value.inMinutes.remainder(60).toString().padLeft(hours > 0 ? 2 : 1, '0');
    final seconds = value.inSeconds.remainder(60).toString().padLeft(2, '0');
    return hours > 0 ? '$hours:$minutes:$seconds' : '$minutes:$seconds';
  }
}
