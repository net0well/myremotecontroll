// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Portuguese (`pt`).
class AppLocalizationsPt extends AppLocalizations {
  AppLocalizationsPt([String locale = 'pt']) : super(locale);

  @override
  String get appTitle => 'MyRemoteControll';

  @override
  String get actionCancel => 'Cancelar';

  @override
  String get actionConfirm => 'Confirmar';

  @override
  String get actionRetry => 'Tentar de novo';

  @override
  String get actionSave => 'Salvar';

  @override
  String get actionClose => 'Fechar';

  @override
  String get actionRemove => 'Remover';

  @override
  String get actionRename => 'Renomear';

  @override
  String get loading => 'Carregando…';

  @override
  String get comingSoon => 'Em breve';

  @override
  String get comingSoonHint => 'Disponível em uma próxima versão.';

  @override
  String get valueUnavailable => '—';

  @override
  String durationSeconds(int count) {
    return '$count s';
  }

  @override
  String durationMinutes(int count) {
    return '$count min';
  }

  @override
  String durationHours(int count) {
    return '$count h';
  }

  @override
  String durationDays(int count) {
    String _temp0 = intl.Intl.pluralLogic(count, locale: localeName, other: '$count dias', one: '1 dia');
    return '$_temp0';
  }

  @override
  String staleSince(String duration) {
    return 'há $duration';
  }

  @override
  String get connectionConnecting => 'Conectando…';

  @override
  String get connectionOnline => 'Online';

  @override
  String get connectionReconnecting => 'Reconectando…';

  @override
  String get connectionAgentStopped => 'Agente encerrado no PC';

  @override
  String get connectionOffline => 'Offline';

  @override
  String connectionOfflineSince(String duration) {
    return 'Offline há $duration';
  }

  @override
  String get connectionRevoked => 'Aparelho revogado';

  @override
  String get connectionRevokedHint => 'Este aparelho foi revogado no PC. Pareie novamente para voltar a usá-lo.';

  @override
  String get connectionCertificateMismatch => 'Alerta de segurança';

  @override
  String get connectionCertificateMismatchHint =>
      'O certificado apresentado pelo PC é diferente do que foi pareado. Isso pode indicar uma tentativa de ataque, por isso a conexão foi bloqueada. Se você reinstalou o agente no PC, pareie novamente.';

  @override
  String get connectionRepair => 'Parear novamente';

  @override
  String get liveLabel => 'Ao vivo';

  @override
  String get pairingTitle => 'Parear com um PC';

  @override
  String get pairingIntro =>
      'No PC, clique no ícone do MyRemoteControll na bandeja e escolha \"Parear aparelho…\". Depois escaneie o código exibido.';

  @override
  String get pairingScan => 'Escanear QR';

  @override
  String get pairingManual => 'Colar link';

  @override
  String get pairingManualHint => 'Cole o link mrc://pair?… exibido no PC';

  @override
  String get pairingDeviceName => 'Nome deste aparelho';

  @override
  String get pairingDeviceNameDefault => 'Meu celular';

  @override
  String get pairingStart => 'Parear';

  @override
  String get pairingWaitingConfirmation => 'Confirme o pareamento na tela do PC…';

  @override
  String pairingSuccess(String pcName) {
    return 'Pareado com $pcName';
  }

  @override
  String get pairingInvalidLink => 'Este não é um código de pareamento do MyRemoteControll.';

  @override
  String get pairingUnreachable =>
      'Não foi possível alcançar o PC. Confira se o celular está na mesma rede do PC ou conectado ao Tailscale.';

  @override
  String get pairingCameraDenied => 'Permita o acesso à câmera para escanear o código, ou cole o link.';

  @override
  String get errorPairingInvalidCode => 'Código de pareamento inválido.';

  @override
  String get errorPairingExpired => 'O código expirou. Gere um novo QR no PC.';

  @override
  String get errorPairingConsumed => 'Este código já foi usado. Gere um novo QR no PC.';

  @override
  String get errorPairingRejected => 'O pareamento foi recusado no PC.';

  @override
  String get errorPairingConfirmationTimeout =>
      'Ninguém confirmou o pareamento no PC a tempo. Gere um novo QR e tente de novo.';

  @override
  String get errorPairingLocked => 'O pareamento está bloqueado por excesso de tentativas. Tente de novo mais tarde.';

  @override
  String get errorForbidden => 'Este aparelho não tem permissão para esta ação.';

  @override
  String get errorUnauthorized => 'Autenticação necessária. Pareie o aparelho novamente.';

  @override
  String get errorPaused => 'O acesso remoto está pausado no PC.';

  @override
  String get errorLastAdmin =>
      'Este é o único administrador. Promova outro aparelho antes de rebaixar ou revogar este.';

  @override
  String get errorValidation => 'Dados inválidos.';

  @override
  String get errorNetwork => 'Sem conexão com o PC.';

  @override
  String get errorUnknown => 'Algo deu errado. Tente de novo.';

  @override
  String get lockTitle => 'MyRemoteControll bloqueado';

  @override
  String get lockUnlock => 'Desbloquear';

  @override
  String get lockReason => 'Desbloqueie para acessar seus PCs';

  @override
  String get lockUnavailable => 'Configure biometria ou um PIN no aparelho para usar o bloqueio.';

  @override
  String get dashboardTitle => 'Painel';

  @override
  String get dashboardNoPc => 'Nenhum PC pareado';

  @override
  String get dashboardPairFirst => 'Pareie um PC para começar.';

  @override
  String get cardPc => 'PC e conexão';

  @override
  String get cardPower => 'Energia';

  @override
  String get cardCpu => 'CPU';

  @override
  String get cardMemory => 'Memória';

  @override
  String get cardNetwork => 'Rede';

  @override
  String get cardDisks => 'Discos';

  @override
  String get cardGpu => 'GPU';

  @override
  String get cardBattery => 'Bateria';

  @override
  String get cardClaude => 'Claude Code';

  @override
  String get cardOpenDetails => 'Ver detalhes';

  @override
  String pcUptime(String duration) {
    return 'Ligado há $duration';
  }

  @override
  String get pcOnBattery => 'Na bateria';

  @override
  String pcPowerPlan(String plan) {
    return 'Plano de energia: $plan';
  }

  @override
  String pcUser(String user) {
    return 'Usuário: $user';
  }

  @override
  String cpuClock(String clock) {
    return 'Clock $clock';
  }

  @override
  String cpuCores(int count) {
    return '$count núcleos lógicos';
  }

  @override
  String cpuCore(int index) {
    return 'Núcleo $index';
  }

  @override
  String memoryUsage(String used, String total) {
    return '$used de $total';
  }

  @override
  String networkDown(String rate) {
    return '↓ $rate';
  }

  @override
  String networkUp(String rate) {
    return '↑ $rate';
  }

  @override
  String get networkTailscale => 'Tailscale';

  @override
  String diskFree(String free, String total) {
    return '$free livres de $total';
  }

  @override
  String diskIo(String read, String write) {
    return 'Leitura $read · Escrita $write';
  }

  @override
  String gpuVram(String used) {
    return 'VRAM $used';
  }

  @override
  String get batteryCharging => 'Carregando';

  @override
  String batteryRemaining(String duration) {
    return 'Restam $duration';
  }

  @override
  String get unavailableHint => 'Contador indisponível neste PC';

  @override
  String get severityAttention => 'Atenção';

  @override
  String get severityCritical => 'Crítico';

  @override
  String get powerLock => 'Bloquear';

  @override
  String get powerSleep => 'Suspender';

  @override
  String get powerRestart => 'Reiniciar';

  @override
  String get powerHibernate => 'Hibernar';

  @override
  String get powerShutdown => 'Desligar';

  @override
  String get powerCancel => 'Cancelar ação';

  @override
  String get powerTitleLock => 'Bloquear a sessão';

  @override
  String get powerTitleSleep => 'Suspender o PC';

  @override
  String get powerTitleRestart => 'Reiniciar o PC';

  @override
  String get powerTitleHibernate => 'Hibernar o PC';

  @override
  String get powerTitleShutdown => 'Desligar o PC';

  @override
  String get powerShutdownWarning =>
      'Depois de desligado, o PC só poderá ser ligado presencialmente: não há como ligá-lo pelo app. Se não precisa desligar, prefira reiniciar ou bloquear.';

  @override
  String get powerUseRestart => 'Reiniciar em vez disso';

  @override
  String get powerUseLock => 'Bloquear em vez disso';

  @override
  String get powerDelay => 'Quando';

  @override
  String get powerDelayNow => 'Agora';

  @override
  String get powerDelayCustom => 'Personalizado';

  @override
  String get powerDelayCustomTitle => 'Atraso personalizado';

  @override
  String get powerDelayCustomHint => 'Minutos (1 a 1440)';

  @override
  String get powerDelayCustomInvalid => 'Informe de 1 a 1440 minutos (24 h).';

  @override
  String powerDelayHoursMinutes(int hours, int minutes) {
    return '$hours h $minutes min';
  }

  @override
  String get powerForce => 'Forçar fechamento dos apps';

  @override
  String get powerForceHint => 'Trabalho não salvo será perdido.';

  @override
  String get powerSlide => 'Deslize para confirmar';

  @override
  String get powerReason => 'Confirme a ação de energia';

  @override
  String get powerAlreadyScheduledTitle => 'Já existe uma ação agendada';

  @override
  String get powerAlreadyScheduledMessage => 'Cancelar a ação agendada e enviar esta no lugar?';

  @override
  String get powerReplace => 'Substituir';

  @override
  String get powerActiveWorkTitle => 'Há trabalho em andamento';

  @override
  String get powerActiveWorkMessage => 'Isto será interrompido:';

  @override
  String get powerContinueAnyway => 'Continuar mesmo assim';

  @override
  String get powerSleepUnavailable => 'Este PC não oferece suspensão.';

  @override
  String get powerHibernateUnavailable => 'A hibernação está desativada neste PC.';

  @override
  String get powerNothingToCancel => 'Não há ação de energia agendada.';

  @override
  String get powerUnavailable => 'Indisponível neste PC';

  @override
  String powerPendingLock(String time) {
    return 'Bloqueia em $time';
  }

  @override
  String powerPendingSleep(String time) {
    return 'Suspende em $time';
  }

  @override
  String powerPendingRestart(String time) {
    return 'Reinicia em $time';
  }

  @override
  String powerPendingHibernate(String time) {
    return 'Hiberna em $time';
  }

  @override
  String powerPendingShutdown(String time) {
    return 'Desliga em $time';
  }

  @override
  String powerRequestedBy(String name) {
    return 'Pedido por $name';
  }

  @override
  String get powerForced => 'Os apps serão fechados à força.';

  @override
  String get powerUpdatePending => 'Há atualizações do Windows pendentes; pode demorar.';

  @override
  String get powerCancelledDevice => 'A ação de energia foi cancelada.';

  @override
  String get powerCancelledTray => 'A ação de energia foi cancelada no PC.';

  @override
  String get powerCancelledAgent => 'O agente foi encerrado no PC; a ação de energia foi descartada.';

  @override
  String get powerCancelledFailed => 'O Windows recusou a ação de energia.';

  @override
  String get pcSwitcherTitle => 'Seus PCs';

  @override
  String get pcAdd => 'Parear outro PC';

  @override
  String get pcAlias => 'Apelido';

  @override
  String pcRemoveConfirm(String pcName) {
    return 'Remover $pcName deste aparelho? Será preciso parear de novo.';
  }

  @override
  String get settingsTitle => 'Configurações';

  @override
  String get settingsPcs => 'PCs pareados';

  @override
  String get settingsAppearance => 'Aparência';

  @override
  String get settingsTheme => 'Tema';

  @override
  String get themeSystem => 'Seguir o sistema';

  @override
  String get themeDark => 'Escuro';

  @override
  String get themeLight => 'Claro';

  @override
  String get settingsDynamicColor => 'Usar as cores do sistema (Material You)';

  @override
  String get settingsSecurity => 'Segurança';

  @override
  String get settingsBiometricLock => 'Bloquear com biometria ou PIN';

  @override
  String settingsLockTimeout(int minutes) {
    return 'Bloquear após $minutes min em segundo plano';
  }

  @override
  String get securityDevices => 'Aparelhos';

  @override
  String get securityAudit => 'Auditoria';

  @override
  String get devicesConnected => 'Conectado agora';

  @override
  String devicesLastSeen(String duration) {
    return 'Visto há $duration';
  }

  @override
  String get devicesNeverSeen => 'Nunca conectado';

  @override
  String get devicesRevoked => 'Revogado';

  @override
  String get devicesRevoke => 'Revogar';

  @override
  String devicesRevokeConfirm(String name) {
    return 'Revogar \"$name\"? O aparelho será desconectado e precisará parear de novo.';
  }

  @override
  String get devicesRole => 'Papel';

  @override
  String get devicesThisDevice => 'Este aparelho';

  @override
  String get roleReadOnly => 'Somente leitura';

  @override
  String get roleOperator => 'Operador';

  @override
  String get roleAdministrator => 'Administrador';

  @override
  String get auditEmpty => 'Nenhum registro.';

  @override
  String get auditLoadMore => 'Carregar mais';

  @override
  String get auditResultSuccess => 'Sucesso';

  @override
  String get auditResultFailed => 'Falhou';

  @override
  String get auditResultDenied => 'Negado';

  @override
  String get confirmSlide => 'Deslize para confirmar';

  @override
  String get confirmReason => 'Confirme para continuar';
}
