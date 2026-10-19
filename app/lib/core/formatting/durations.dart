import '../../l10n/app_localizations.dart';

/// Duração curta para "há 12 s", "Offline há 5 min", "Visto há 2 dias".
String formatDuration(AppLocalizations l10n, Duration duration) {
  if (duration.inSeconds < 60) {
    return l10n.durationSeconds(duration.inSeconds < 0 ? 0 : duration.inSeconds);
  }

  if (duration.inMinutes < 60) {
    return l10n.durationMinutes(duration.inMinutes);
  }

  if (duration.inHours < 24) {
    return l10n.durationHours(duration.inHours);
  }

  return l10n.durationDays(duration.inDays);
}
