import '../../l10n/app_localizations.dart';
import '../contracts/contracts.dart';

/// Tipos de falha que mudam o que o app mostra (spec 004): cada um vira um estado de conexão ou uma mensagem.
enum AgentFailureKind {
  /// Sem rota até o PC (rede, PC desligado, Tailscale desligado).
  unreachable,

  /// O PC apresentou um certificado com pin diferente do pareado: possível ataque (spec 002, AC-3).
  certificateMismatch,

  /// Token não vale mais (revogado ou desconhecido).
  unauthorized,

  /// O agente respondeu com erro de negócio (ProblemDetails com `code`).
  rejected,
}

/// Erro vindo do agente ou do caminho até ele, com o código estável do contrato quando houver.
class AgentException implements Exception {
  const AgentException(this.kind, {this.code, this.statusCode, this.serverMessage});

  final AgentFailureKind kind;
  final String? code;
  final int? statusCode;

  /// Texto do agente (já em pt-BR); usado só quando não há tradução própria para o [code].
  final String? serverMessage;

  bool get isRevoked => code == ErrorCodes.authRevoked;

  /// Mensagem para o usuário, sempre em pt-BR.
  String message(AppLocalizations l10n) => switch (kind) {
    AgentFailureKind.unreachable => l10n.errorNetwork,
    AgentFailureKind.certificateMismatch => l10n.connectionCertificateMismatchHint,
    AgentFailureKind.unauthorized => isRevoked ? l10n.connectionRevokedHint : l10n.errorUnauthorized,
    AgentFailureKind.rejected => switch (code) {
      ErrorCodes.pairingInvalidCode => l10n.errorPairingInvalidCode,
      ErrorCodes.pairingExpired => l10n.errorPairingExpired,
      ErrorCodes.pairingConsumed => l10n.errorPairingConsumed,
      ErrorCodes.pairingRejected => l10n.errorPairingRejected,
      ErrorCodes.pairingConfirmationTimeout => l10n.errorPairingConfirmationTimeout,
      ErrorCodes.pairingLocked => l10n.errorPairingLocked,
      ErrorCodes.authForbidden => l10n.errorForbidden,
      ErrorCodes.agentPaused => l10n.errorPaused,
      ErrorCodes.securityLastAdmin => l10n.errorLastAdmin,
      ErrorCodes.validationFailed => l10n.errorValidation,
      _ => serverMessage ?? l10n.errorUnknown,
    },
  };

  @override
  String toString() => 'AgentException($kind, code: $code, status: $statusCode)';
}
