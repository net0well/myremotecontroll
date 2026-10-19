import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_pt.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'l10n/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale) : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate = _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates = <LocalizationsDelegate<dynamic>>[
    delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
  ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[Locale('pt')];

  /// No description provided for @appTitle.
  ///
  /// In pt, this message translates to:
  /// **'MyRemoteControll'**
  String get appTitle;

  /// No description provided for @actionCancel.
  ///
  /// In pt, this message translates to:
  /// **'Cancelar'**
  String get actionCancel;

  /// No description provided for @actionConfirm.
  ///
  /// In pt, this message translates to:
  /// **'Confirmar'**
  String get actionConfirm;

  /// No description provided for @actionRetry.
  ///
  /// In pt, this message translates to:
  /// **'Tentar de novo'**
  String get actionRetry;

  /// No description provided for @actionSave.
  ///
  /// In pt, this message translates to:
  /// **'Salvar'**
  String get actionSave;

  /// No description provided for @actionClose.
  ///
  /// In pt, this message translates to:
  /// **'Fechar'**
  String get actionClose;

  /// No description provided for @actionRemove.
  ///
  /// In pt, this message translates to:
  /// **'Remover'**
  String get actionRemove;

  /// No description provided for @actionRename.
  ///
  /// In pt, this message translates to:
  /// **'Renomear'**
  String get actionRename;

  /// No description provided for @loading.
  ///
  /// In pt, this message translates to:
  /// **'Carregando…'**
  String get loading;

  /// No description provided for @comingSoon.
  ///
  /// In pt, this message translates to:
  /// **'Em breve'**
  String get comingSoon;

  /// No description provided for @comingSoonHint.
  ///
  /// In pt, this message translates to:
  /// **'Disponível em uma próxima versão.'**
  String get comingSoonHint;

  /// No description provided for @valueUnavailable.
  ///
  /// In pt, this message translates to:
  /// **'—'**
  String get valueUnavailable;

  /// No description provided for @durationSeconds.
  ///
  /// In pt, this message translates to:
  /// **'{count} s'**
  String durationSeconds(int count);

  /// No description provided for @durationMinutes.
  ///
  /// In pt, this message translates to:
  /// **'{count} min'**
  String durationMinutes(int count);

  /// No description provided for @durationHours.
  ///
  /// In pt, this message translates to:
  /// **'{count} h'**
  String durationHours(int count);

  /// No description provided for @durationDays.
  ///
  /// In pt, this message translates to:
  /// **'{count, plural, =1{1 dia} other{{count} dias}}'**
  String durationDays(int count);

  /// No description provided for @staleSince.
  ///
  /// In pt, this message translates to:
  /// **'há {duration}'**
  String staleSince(String duration);

  /// No description provided for @connectionConnecting.
  ///
  /// In pt, this message translates to:
  /// **'Conectando…'**
  String get connectionConnecting;

  /// No description provided for @connectionOnline.
  ///
  /// In pt, this message translates to:
  /// **'Online'**
  String get connectionOnline;

  /// No description provided for @connectionReconnecting.
  ///
  /// In pt, this message translates to:
  /// **'Reconectando…'**
  String get connectionReconnecting;

  /// No description provided for @connectionAgentStopped.
  ///
  /// In pt, this message translates to:
  /// **'Agente encerrado no PC'**
  String get connectionAgentStopped;

  /// No description provided for @connectionOffline.
  ///
  /// In pt, this message translates to:
  /// **'Offline'**
  String get connectionOffline;

  /// No description provided for @connectionOfflineSince.
  ///
  /// In pt, this message translates to:
  /// **'Offline há {duration}'**
  String connectionOfflineSince(String duration);

  /// No description provided for @connectionRevoked.
  ///
  /// In pt, this message translates to:
  /// **'Aparelho revogado'**
  String get connectionRevoked;

  /// No description provided for @connectionRevokedHint.
  ///
  /// In pt, this message translates to:
  /// **'Este aparelho foi revogado no PC. Pareie novamente para voltar a usá-lo.'**
  String get connectionRevokedHint;

  /// No description provided for @connectionCertificateMismatch.
  ///
  /// In pt, this message translates to:
  /// **'Alerta de segurança'**
  String get connectionCertificateMismatch;

  /// No description provided for @connectionCertificateMismatchHint.
  ///
  /// In pt, this message translates to:
  /// **'O certificado apresentado pelo PC é diferente do que foi pareado. Isso pode indicar uma tentativa de ataque, por isso a conexão foi bloqueada. Se você reinstalou o agente no PC, pareie novamente.'**
  String get connectionCertificateMismatchHint;

  /// No description provided for @connectionRepair.
  ///
  /// In pt, this message translates to:
  /// **'Parear novamente'**
  String get connectionRepair;

  /// No description provided for @liveLabel.
  ///
  /// In pt, this message translates to:
  /// **'Ao vivo'**
  String get liveLabel;

  /// No description provided for @pairingTitle.
  ///
  /// In pt, this message translates to:
  /// **'Parear com um PC'**
  String get pairingTitle;

  /// No description provided for @pairingIntro.
  ///
  /// In pt, this message translates to:
  /// **'No PC, clique no ícone do MyRemoteControll na bandeja e escolha \"Parear aparelho…\". Depois escaneie o código exibido.'**
  String get pairingIntro;

  /// No description provided for @pairingScan.
  ///
  /// In pt, this message translates to:
  /// **'Escanear QR'**
  String get pairingScan;

  /// No description provided for @pairingManual.
  ///
  /// In pt, this message translates to:
  /// **'Colar link'**
  String get pairingManual;

  /// No description provided for @pairingManualHint.
  ///
  /// In pt, this message translates to:
  /// **'Cole o link mrc://pair?… exibido no PC'**
  String get pairingManualHint;

  /// No description provided for @pairingDeviceName.
  ///
  /// In pt, this message translates to:
  /// **'Nome deste aparelho'**
  String get pairingDeviceName;

  /// No description provided for @pairingDeviceNameDefault.
  ///
  /// In pt, this message translates to:
  /// **'Meu celular'**
  String get pairingDeviceNameDefault;

  /// No description provided for @pairingStart.
  ///
  /// In pt, this message translates to:
  /// **'Parear'**
  String get pairingStart;

  /// No description provided for @pairingWaitingConfirmation.
  ///
  /// In pt, this message translates to:
  /// **'Confirme o pareamento na tela do PC…'**
  String get pairingWaitingConfirmation;

  /// No description provided for @pairingSuccess.
  ///
  /// In pt, this message translates to:
  /// **'Pareado com {pcName}'**
  String pairingSuccess(String pcName);

  /// No description provided for @pairingInvalidLink.
  ///
  /// In pt, this message translates to:
  /// **'Este não é um código de pareamento do MyRemoteControll.'**
  String get pairingInvalidLink;

  /// No description provided for @pairingUnreachable.
  ///
  /// In pt, this message translates to:
  /// **'Não foi possível alcançar o PC. Confira se o celular está na mesma rede do PC ou conectado ao Tailscale.'**
  String get pairingUnreachable;

  /// No description provided for @pairingCameraDenied.
  ///
  /// In pt, this message translates to:
  /// **'Permita o acesso à câmera para escanear o código, ou cole o link.'**
  String get pairingCameraDenied;

  /// No description provided for @errorPairingInvalidCode.
  ///
  /// In pt, this message translates to:
  /// **'Código de pareamento inválido.'**
  String get errorPairingInvalidCode;

  /// No description provided for @errorPairingExpired.
  ///
  /// In pt, this message translates to:
  /// **'O código expirou. Gere um novo QR no PC.'**
  String get errorPairingExpired;

  /// No description provided for @errorPairingConsumed.
  ///
  /// In pt, this message translates to:
  /// **'Este código já foi usado. Gere um novo QR no PC.'**
  String get errorPairingConsumed;

  /// No description provided for @errorPairingRejected.
  ///
  /// In pt, this message translates to:
  /// **'O pareamento foi recusado no PC.'**
  String get errorPairingRejected;

  /// No description provided for @errorPairingConfirmationTimeout.
  ///
  /// In pt, this message translates to:
  /// **'Ninguém confirmou o pareamento no PC a tempo. Gere um novo QR e tente de novo.'**
  String get errorPairingConfirmationTimeout;

  /// No description provided for @errorPairingLocked.
  ///
  /// In pt, this message translates to:
  /// **'O pareamento está bloqueado por excesso de tentativas. Tente de novo mais tarde.'**
  String get errorPairingLocked;

  /// No description provided for @errorForbidden.
  ///
  /// In pt, this message translates to:
  /// **'Este aparelho não tem permissão para esta ação.'**
  String get errorForbidden;

  /// No description provided for @errorUnauthorized.
  ///
  /// In pt, this message translates to:
  /// **'Autenticação necessária. Pareie o aparelho novamente.'**
  String get errorUnauthorized;

  /// No description provided for @errorPaused.
  ///
  /// In pt, this message translates to:
  /// **'O acesso remoto está pausado no PC.'**
  String get errorPaused;

  /// No description provided for @errorLastAdmin.
  ///
  /// In pt, this message translates to:
  /// **'Este é o único administrador. Promova outro aparelho antes de rebaixar ou revogar este.'**
  String get errorLastAdmin;

  /// No description provided for @errorValidation.
  ///
  /// In pt, this message translates to:
  /// **'Dados inválidos.'**
  String get errorValidation;

  /// No description provided for @errorNetwork.
  ///
  /// In pt, this message translates to:
  /// **'Sem conexão com o PC.'**
  String get errorNetwork;

  /// No description provided for @errorUnknown.
  ///
  /// In pt, this message translates to:
  /// **'Algo deu errado. Tente de novo.'**
  String get errorUnknown;

  /// No description provided for @lockTitle.
  ///
  /// In pt, this message translates to:
  /// **'MyRemoteControll bloqueado'**
  String get lockTitle;

  /// No description provided for @lockUnlock.
  ///
  /// In pt, this message translates to:
  /// **'Desbloquear'**
  String get lockUnlock;

  /// No description provided for @lockReason.
  ///
  /// In pt, this message translates to:
  /// **'Desbloqueie para acessar seus PCs'**
  String get lockReason;

  /// No description provided for @lockUnavailable.
  ///
  /// In pt, this message translates to:
  /// **'Configure biometria ou um PIN no aparelho para usar o bloqueio.'**
  String get lockUnavailable;

  /// No description provided for @dashboardTitle.
  ///
  /// In pt, this message translates to:
  /// **'Painel'**
  String get dashboardTitle;

  /// No description provided for @dashboardNoPc.
  ///
  /// In pt, this message translates to:
  /// **'Nenhum PC pareado'**
  String get dashboardNoPc;

  /// No description provided for @dashboardPairFirst.
  ///
  /// In pt, this message translates to:
  /// **'Pareie um PC para começar.'**
  String get dashboardPairFirst;

  /// No description provided for @cardPc.
  ///
  /// In pt, this message translates to:
  /// **'PC e conexão'**
  String get cardPc;

  /// No description provided for @cardPower.
  ///
  /// In pt, this message translates to:
  /// **'Energia'**
  String get cardPower;

  /// No description provided for @cardCpu.
  ///
  /// In pt, this message translates to:
  /// **'CPU'**
  String get cardCpu;

  /// No description provided for @cardMemory.
  ///
  /// In pt, this message translates to:
  /// **'Memória'**
  String get cardMemory;

  /// No description provided for @cardNetwork.
  ///
  /// In pt, this message translates to:
  /// **'Rede'**
  String get cardNetwork;

  /// No description provided for @cardDisks.
  ///
  /// In pt, this message translates to:
  /// **'Discos'**
  String get cardDisks;

  /// No description provided for @cardGpu.
  ///
  /// In pt, this message translates to:
  /// **'GPU'**
  String get cardGpu;

  /// No description provided for @cardBattery.
  ///
  /// In pt, this message translates to:
  /// **'Bateria'**
  String get cardBattery;

  /// No description provided for @cardClaude.
  ///
  /// In pt, this message translates to:
  /// **'Claude Code'**
  String get cardClaude;

  /// No description provided for @cardOpenDetails.
  ///
  /// In pt, this message translates to:
  /// **'Ver detalhes'**
  String get cardOpenDetails;

  /// No description provided for @pcUptime.
  ///
  /// In pt, this message translates to:
  /// **'Ligado há {duration}'**
  String pcUptime(String duration);

  /// No description provided for @pcOnBattery.
  ///
  /// In pt, this message translates to:
  /// **'Na bateria'**
  String get pcOnBattery;

  /// No description provided for @pcPowerPlan.
  ///
  /// In pt, this message translates to:
  /// **'Plano de energia: {plan}'**
  String pcPowerPlan(String plan);

  /// No description provided for @pcUser.
  ///
  /// In pt, this message translates to:
  /// **'Usuário: {user}'**
  String pcUser(String user);

  /// No description provided for @cpuClock.
  ///
  /// In pt, this message translates to:
  /// **'Clock {clock}'**
  String cpuClock(String clock);

  /// No description provided for @cpuCores.
  ///
  /// In pt, this message translates to:
  /// **'{count} núcleos lógicos'**
  String cpuCores(int count);

  /// No description provided for @cpuCore.
  ///
  /// In pt, this message translates to:
  /// **'Núcleo {index}'**
  String cpuCore(int index);

  /// No description provided for @memoryUsage.
  ///
  /// In pt, this message translates to:
  /// **'{used} de {total}'**
  String memoryUsage(String used, String total);

  /// No description provided for @networkDown.
  ///
  /// In pt, this message translates to:
  /// **'↓ {rate}'**
  String networkDown(String rate);

  /// No description provided for @networkUp.
  ///
  /// In pt, this message translates to:
  /// **'↑ {rate}'**
  String networkUp(String rate);

  /// No description provided for @networkTailscale.
  ///
  /// In pt, this message translates to:
  /// **'Tailscale'**
  String get networkTailscale;

  /// No description provided for @diskFree.
  ///
  /// In pt, this message translates to:
  /// **'{free} livres de {total}'**
  String diskFree(String free, String total);

  /// No description provided for @diskIo.
  ///
  /// In pt, this message translates to:
  /// **'Leitura {read} · Escrita {write}'**
  String diskIo(String read, String write);

  /// No description provided for @gpuVram.
  ///
  /// In pt, this message translates to:
  /// **'VRAM {used}'**
  String gpuVram(String used);

  /// No description provided for @batteryCharging.
  ///
  /// In pt, this message translates to:
  /// **'Carregando'**
  String get batteryCharging;

  /// No description provided for @batteryRemaining.
  ///
  /// In pt, this message translates to:
  /// **'Restam {duration}'**
  String batteryRemaining(String duration);

  /// No description provided for @unavailableHint.
  ///
  /// In pt, this message translates to:
  /// **'Contador indisponível neste PC'**
  String get unavailableHint;

  /// No description provided for @severityAttention.
  ///
  /// In pt, this message translates to:
  /// **'Atenção'**
  String get severityAttention;

  /// No description provided for @severityCritical.
  ///
  /// In pt, this message translates to:
  /// **'Crítico'**
  String get severityCritical;

  /// No description provided for @powerLock.
  ///
  /// In pt, this message translates to:
  /// **'Bloquear'**
  String get powerLock;

  /// No description provided for @powerSleep.
  ///
  /// In pt, this message translates to:
  /// **'Suspender'**
  String get powerSleep;

  /// No description provided for @powerRestart.
  ///
  /// In pt, this message translates to:
  /// **'Reiniciar'**
  String get powerRestart;

  /// No description provided for @powerHibernate.
  ///
  /// In pt, this message translates to:
  /// **'Hibernar'**
  String get powerHibernate;

  /// No description provided for @powerShutdown.
  ///
  /// In pt, this message translates to:
  /// **'Desligar'**
  String get powerShutdown;

  /// No description provided for @powerCancel.
  ///
  /// In pt, this message translates to:
  /// **'Cancelar ação'**
  String get powerCancel;

  /// No description provided for @powerTitleLock.
  ///
  /// In pt, this message translates to:
  /// **'Bloquear a sessão'**
  String get powerTitleLock;

  /// No description provided for @powerTitleSleep.
  ///
  /// In pt, this message translates to:
  /// **'Suspender o PC'**
  String get powerTitleSleep;

  /// No description provided for @powerTitleRestart.
  ///
  /// In pt, this message translates to:
  /// **'Reiniciar o PC'**
  String get powerTitleRestart;

  /// No description provided for @powerTitleHibernate.
  ///
  /// In pt, this message translates to:
  /// **'Hibernar o PC'**
  String get powerTitleHibernate;

  /// No description provided for @powerTitleShutdown.
  ///
  /// In pt, this message translates to:
  /// **'Desligar o PC'**
  String get powerTitleShutdown;

  /// No description provided for @powerShutdownWarning.
  ///
  /// In pt, this message translates to:
  /// **'Depois de desligado, o PC só poderá ser ligado presencialmente: não há como ligá-lo pelo app. Se não precisa desligar, prefira reiniciar ou bloquear.'**
  String get powerShutdownWarning;

  /// No description provided for @powerUseRestart.
  ///
  /// In pt, this message translates to:
  /// **'Reiniciar em vez disso'**
  String get powerUseRestart;

  /// No description provided for @powerUseLock.
  ///
  /// In pt, this message translates to:
  /// **'Bloquear em vez disso'**
  String get powerUseLock;

  /// No description provided for @powerDelay.
  ///
  /// In pt, this message translates to:
  /// **'Quando'**
  String get powerDelay;

  /// No description provided for @powerDelayNow.
  ///
  /// In pt, this message translates to:
  /// **'Agora'**
  String get powerDelayNow;

  /// No description provided for @powerDelayCustom.
  ///
  /// In pt, this message translates to:
  /// **'Personalizado'**
  String get powerDelayCustom;

  /// No description provided for @powerDelayCustomTitle.
  ///
  /// In pt, this message translates to:
  /// **'Atraso personalizado'**
  String get powerDelayCustomTitle;

  /// No description provided for @powerDelayCustomHint.
  ///
  /// In pt, this message translates to:
  /// **'Minutos (1 a 1440)'**
  String get powerDelayCustomHint;

  /// No description provided for @powerDelayCustomInvalid.
  ///
  /// In pt, this message translates to:
  /// **'Informe de 1 a 1440 minutos (24 h).'**
  String get powerDelayCustomInvalid;

  /// No description provided for @powerDelayHoursMinutes.
  ///
  /// In pt, this message translates to:
  /// **'{hours} h {minutes} min'**
  String powerDelayHoursMinutes(int hours, int minutes);

  /// No description provided for @powerForce.
  ///
  /// In pt, this message translates to:
  /// **'Forçar fechamento dos apps'**
  String get powerForce;

  /// No description provided for @powerForceHint.
  ///
  /// In pt, this message translates to:
  /// **'Trabalho não salvo será perdido.'**
  String get powerForceHint;

  /// No description provided for @powerSlide.
  ///
  /// In pt, this message translates to:
  /// **'Deslize para confirmar'**
  String get powerSlide;

  /// No description provided for @powerReason.
  ///
  /// In pt, this message translates to:
  /// **'Confirme a ação de energia'**
  String get powerReason;

  /// No description provided for @powerAlreadyScheduledTitle.
  ///
  /// In pt, this message translates to:
  /// **'Já existe uma ação agendada'**
  String get powerAlreadyScheduledTitle;

  /// No description provided for @powerAlreadyScheduledMessage.
  ///
  /// In pt, this message translates to:
  /// **'Cancelar a ação agendada e enviar esta no lugar?'**
  String get powerAlreadyScheduledMessage;

  /// No description provided for @powerReplace.
  ///
  /// In pt, this message translates to:
  /// **'Substituir'**
  String get powerReplace;

  /// No description provided for @powerActiveWorkTitle.
  ///
  /// In pt, this message translates to:
  /// **'Há trabalho em andamento'**
  String get powerActiveWorkTitle;

  /// No description provided for @powerActiveWorkMessage.
  ///
  /// In pt, this message translates to:
  /// **'Isto será interrompido:'**
  String get powerActiveWorkMessage;

  /// No description provided for @powerContinueAnyway.
  ///
  /// In pt, this message translates to:
  /// **'Continuar mesmo assim'**
  String get powerContinueAnyway;

  /// No description provided for @powerSleepUnavailable.
  ///
  /// In pt, this message translates to:
  /// **'Este PC não oferece suspensão.'**
  String get powerSleepUnavailable;

  /// No description provided for @powerHibernateUnavailable.
  ///
  /// In pt, this message translates to:
  /// **'A hibernação está desativada neste PC.'**
  String get powerHibernateUnavailable;

  /// No description provided for @powerNothingToCancel.
  ///
  /// In pt, this message translates to:
  /// **'Não há ação de energia agendada.'**
  String get powerNothingToCancel;

  /// No description provided for @powerUnavailable.
  ///
  /// In pt, this message translates to:
  /// **'Indisponível neste PC'**
  String get powerUnavailable;

  /// No description provided for @powerPendingLock.
  ///
  /// In pt, this message translates to:
  /// **'Bloqueia em {time}'**
  String powerPendingLock(String time);

  /// No description provided for @powerPendingSleep.
  ///
  /// In pt, this message translates to:
  /// **'Suspende em {time}'**
  String powerPendingSleep(String time);

  /// No description provided for @powerPendingRestart.
  ///
  /// In pt, this message translates to:
  /// **'Reinicia em {time}'**
  String powerPendingRestart(String time);

  /// No description provided for @powerPendingHibernate.
  ///
  /// In pt, this message translates to:
  /// **'Hiberna em {time}'**
  String powerPendingHibernate(String time);

  /// No description provided for @powerPendingShutdown.
  ///
  /// In pt, this message translates to:
  /// **'Desliga em {time}'**
  String powerPendingShutdown(String time);

  /// No description provided for @powerRequestedBy.
  ///
  /// In pt, this message translates to:
  /// **'Pedido por {name}'**
  String powerRequestedBy(String name);

  /// No description provided for @powerForced.
  ///
  /// In pt, this message translates to:
  /// **'Os apps serão fechados à força.'**
  String get powerForced;

  /// No description provided for @powerUpdatePending.
  ///
  /// In pt, this message translates to:
  /// **'Há atualizações do Windows pendentes; pode demorar.'**
  String get powerUpdatePending;

  /// No description provided for @powerCancelledDevice.
  ///
  /// In pt, this message translates to:
  /// **'A ação de energia foi cancelada.'**
  String get powerCancelledDevice;

  /// No description provided for @powerCancelledTray.
  ///
  /// In pt, this message translates to:
  /// **'A ação de energia foi cancelada no PC.'**
  String get powerCancelledTray;

  /// No description provided for @powerCancelledAgent.
  ///
  /// In pt, this message translates to:
  /// **'O agente foi encerrado no PC; a ação de energia foi descartada.'**
  String get powerCancelledAgent;

  /// No description provided for @powerCancelledFailed.
  ///
  /// In pt, this message translates to:
  /// **'O Windows recusou a ação de energia.'**
  String get powerCancelledFailed;

  /// No description provided for @pcSwitcherTitle.
  ///
  /// In pt, this message translates to:
  /// **'Seus PCs'**
  String get pcSwitcherTitle;

  /// No description provided for @pcAdd.
  ///
  /// In pt, this message translates to:
  /// **'Parear outro PC'**
  String get pcAdd;

  /// No description provided for @pcAlias.
  ///
  /// In pt, this message translates to:
  /// **'Apelido'**
  String get pcAlias;

  /// No description provided for @pcRemoveConfirm.
  ///
  /// In pt, this message translates to:
  /// **'Remover {pcName} deste aparelho? Será preciso parear de novo.'**
  String pcRemoveConfirm(String pcName);

  /// No description provided for @settingsTitle.
  ///
  /// In pt, this message translates to:
  /// **'Configurações'**
  String get settingsTitle;

  /// No description provided for @settingsPcs.
  ///
  /// In pt, this message translates to:
  /// **'PCs pareados'**
  String get settingsPcs;

  /// No description provided for @settingsAppearance.
  ///
  /// In pt, this message translates to:
  /// **'Aparência'**
  String get settingsAppearance;

  /// No description provided for @settingsTheme.
  ///
  /// In pt, this message translates to:
  /// **'Tema'**
  String get settingsTheme;

  /// No description provided for @themeSystem.
  ///
  /// In pt, this message translates to:
  /// **'Seguir o sistema'**
  String get themeSystem;

  /// No description provided for @themeDark.
  ///
  /// In pt, this message translates to:
  /// **'Escuro'**
  String get themeDark;

  /// No description provided for @themeLight.
  ///
  /// In pt, this message translates to:
  /// **'Claro'**
  String get themeLight;

  /// No description provided for @settingsDynamicColor.
  ///
  /// In pt, this message translates to:
  /// **'Usar as cores do sistema (Material You)'**
  String get settingsDynamicColor;

  /// No description provided for @settingsSecurity.
  ///
  /// In pt, this message translates to:
  /// **'Segurança'**
  String get settingsSecurity;

  /// No description provided for @settingsBiometricLock.
  ///
  /// In pt, this message translates to:
  /// **'Bloquear com biometria ou PIN'**
  String get settingsBiometricLock;

  /// No description provided for @settingsLockTimeout.
  ///
  /// In pt, this message translates to:
  /// **'Bloquear após {minutes} min em segundo plano'**
  String settingsLockTimeout(int minutes);

  /// No description provided for @securityDevices.
  ///
  /// In pt, this message translates to:
  /// **'Aparelhos'**
  String get securityDevices;

  /// No description provided for @securityAudit.
  ///
  /// In pt, this message translates to:
  /// **'Auditoria'**
  String get securityAudit;

  /// No description provided for @devicesConnected.
  ///
  /// In pt, this message translates to:
  /// **'Conectado agora'**
  String get devicesConnected;

  /// No description provided for @devicesLastSeen.
  ///
  /// In pt, this message translates to:
  /// **'Visto há {duration}'**
  String devicesLastSeen(String duration);

  /// No description provided for @devicesNeverSeen.
  ///
  /// In pt, this message translates to:
  /// **'Nunca conectado'**
  String get devicesNeverSeen;

  /// No description provided for @devicesRevoked.
  ///
  /// In pt, this message translates to:
  /// **'Revogado'**
  String get devicesRevoked;

  /// No description provided for @devicesRevoke.
  ///
  /// In pt, this message translates to:
  /// **'Revogar'**
  String get devicesRevoke;

  /// No description provided for @devicesRevokeConfirm.
  ///
  /// In pt, this message translates to:
  /// **'Revogar \"{name}\"? O aparelho será desconectado e precisará parear de novo.'**
  String devicesRevokeConfirm(String name);

  /// No description provided for @devicesRole.
  ///
  /// In pt, this message translates to:
  /// **'Papel'**
  String get devicesRole;

  /// No description provided for @devicesThisDevice.
  ///
  /// In pt, this message translates to:
  /// **'Este aparelho'**
  String get devicesThisDevice;

  /// No description provided for @roleReadOnly.
  ///
  /// In pt, this message translates to:
  /// **'Somente leitura'**
  String get roleReadOnly;

  /// No description provided for @roleOperator.
  ///
  /// In pt, this message translates to:
  /// **'Operador'**
  String get roleOperator;

  /// No description provided for @roleAdministrator.
  ///
  /// In pt, this message translates to:
  /// **'Administrador'**
  String get roleAdministrator;

  /// No description provided for @auditEmpty.
  ///
  /// In pt, this message translates to:
  /// **'Nenhum registro.'**
  String get auditEmpty;

  /// No description provided for @auditLoadMore.
  ///
  /// In pt, this message translates to:
  /// **'Carregar mais'**
  String get auditLoadMore;

  /// No description provided for @auditResultSuccess.
  ///
  /// In pt, this message translates to:
  /// **'Sucesso'**
  String get auditResultSuccess;

  /// No description provided for @auditResultFailed.
  ///
  /// In pt, this message translates to:
  /// **'Falhou'**
  String get auditResultFailed;

  /// No description provided for @auditResultDenied.
  ///
  /// In pt, this message translates to:
  /// **'Negado'**
  String get auditResultDenied;

  /// No description provided for @confirmSlide.
  ///
  /// In pt, this message translates to:
  /// **'Deslize para confirmar'**
  String get confirmSlide;

  /// No description provided for @confirmReason.
  ///
  /// In pt, this message translates to:
  /// **'Confirme para continuar'**
  String get confirmReason;
}

class _AppLocalizationsDelegate extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) => <String>['pt'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'pt':
      return AppLocalizationsPt();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
