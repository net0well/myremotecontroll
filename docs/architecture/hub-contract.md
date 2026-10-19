# Contrato da API e do Hub — MyRemoteControll Agent

> **Status:** Aprovado em 2026-10-18. Evolui pelas specs (mudanças passam pelo `/spec`). Base: seção 8 do documento base. ADRs 0005, 0008, 0009, 0021.
> Os tipos ficam em `MyRemoteControll.Contracts` (`IRemoteHub`, `IRemoteClient`, DTOs, `ErrorCodes`, `PermissionKeys`).
> Itens novos em relação ao documento base estão marcados com **[novo]**.

## 1. Convenções

- Base REST: `https://<host>:47800/api/v1/...`; JSON camelCase; erros em **ProblemDetails** com extensão `code` estável.
- Hub: `wss://<host>:47800/hubs/remote`, protocolo JSON, `Hub<IRemoteClient>`.
- Autenticação: `Authorization: Bearer <token>` (REST e negociação); `access_token` na query do WebSocket (padrão
  SignalR; removido dos logs).
- **Retorno dos métodos do Hub** (Result pattern): todo método devolve `HubResponse<T>`:
  ```json
  { "ok": true, "data": { } }
  { "ok": false, "error": { "code": "power.alreadyScheduled", "message": "Já existe uma ação agendada.", "details": { } } }
  ```
  Exceções inesperadas viram `internal.error` sem detalhes (o detalhe vai para o log com o correlation ID).
- Ações destrutivas recebem `localConfirmation: bool` (biometria/PIN feita no app), gravado na auditoria.
- Operações mutáveis REST aceitam `Idempotency-Key`. Paginação `?cursor=&limit=` (máx. 200).
- Cada método declara uma permissão (`[RequirePermission]`); um teste de arquitetura impede método sem permissão.
- Grupos do SignalR: `device:{id}`, `perm:{chave}` (recalculados quando as permissões mudam),
  `claude-session:{id}` (quem abriu o chat), `dashboard` (inscritos no painel), `screen-viewers`.

## 2. REST

| Verbo | Rota | Descrição | Permissão | Spec |
|---|---|---|---|---|
| GET | `/api/v1/info` | Sem token: `apiVersion`, `requiresPairing`. Com token: + `agentVersion`, `pcName`, `capabilities[]` | — / autenticado | 001 |
| GET | `/health` | Só no loopback (na rede: 404) | — | 001 |
| POST | `/api/v1/pairing/complete` | `{code, deviceName, platform}` → `{deviceId, token, role, pcName}` (aguarda confirmação na bandeja) | código de pareamento | 002 |
| POST | `/api/v1/devices/me/rotate-token` | Novo token; o anterior vale 24 h **[novo]** | autenticado | 002 |
| GET | `/api/v1/devices` | Listar aparelhos | `security.manageDevices` | 003 |
| DELETE | `/api/v1/devices/{id}` | Revogar | `security.manageDevices` | 003 |
| GET | `/api/v1/audit?from=&to=&deviceId=&action=&result=&cursor=&limit=` | Consultar | `security.viewAudit` | 003 |
| GET | `/api/v1/audit/export.csv?...` | Exportar CSV | `security.viewAudit` | 003 |
| GET | `/api/v1/screen/screenshot?monitor=` | PNG (`monitor` omitido = todos) | `screen.view` | 017 |
| GET | `/ws/screen?monitor=&quality=&maxFps=` | WebSocket binário de quadros MJPEG **[novo]** (ADR-0005) | `screen.view` | 018 |
| GET | `/api/v1/files/roots` | Raízes permitidas **[novo]** | `files.read` | 020 |
| GET | `/api/v1/files/list?root=&path=&sort=` | Listar diretório | `files.read` | 020 |
| GET | `/api/v1/files/search?root=&path=&q=&content=` | Buscar por nome/conteúdo (com limite) **[novo]** | `files.read` | 020 |
| GET | `/api/v1/files/download?root=&path=` | Download com `Range` | `files.read` | 020 |
| GET | `/api/v1/files/preview?root=&path=` | Texto/código (limite de tamanho) | `files.read` | 020 |
| POST | `/api/v1/files/upload?root=&path=` | Upload em streaming (conflito → renomeia) | `files.write` | 020 |
| POST | `/api/v1/files/ops` | `rename`, `move`, `copy`, `mkdir`, `delete` (Lixeira) | `files.write` / `files.delete` | 020 |
| GET | `/api/v1/builds/{runId}/log` | Log completo | `dev.builds` | 014 |
| GET | `/api/v1/metrics/history?kind=&key=&from=&to=&resolution=` | Histórico | `monitor.view` | 024 |
| ANY | `/preview/{port}/{**path}` | Proxy YARP (cookie obtido com ticket) | `dev.preview` | 015 |

## 3. Hub — cliente → servidor (`IRemoteHub`)

### Painel (ADR-0021, specs 007/028)
| Método | Retorno | Permissão |
|---|---|---|
| `SubscribeDashboard(DashboardOptions options)` | `DashboardSnapshot` | autenticado (cards filtrados) |
| `UnsubscribeDashboard()` | `bool` | autenticado |
| `RequestDashboardSnapshot()` | `DashboardSnapshot` | autenticado |
| `SetDashboardRate(int intervalMs)` | `bool` | autenticado (0,5–10 s; fora disso, `validation.failed`) |

### Status (spec 007)
| Método | Retorno | Permissão |
|---|---|---|
| `GetStatus()` | `SystemStatus` | `status.view` |
| `SubscribeMetrics(int intervalMs)` / `UnsubscribeMetrics()` | `bool` | `monitor.view` |

### Energia (spec 005)
| Método | Retorno | Permissão |
|---|---|---|
| `PowerAction(PowerActionRequest req)` | `PowerCountdownDto` | `power.basic` (Lock, Sleep) / `power.shutdown` (Shutdown, Restart, Hibernate) |
| `CancelPowerAction()` | `PowerCancelledDto` | `power.basic` |

`PowerActionRequest { action, delaySeconds, force, acknowledgeActiveWork, localConfirmation }`. Com sessões do Claude
trabalhando ou builds rodando e `acknowledgeActiveWork = false`, retorna `power.activeWork` com a lista **[novo]**.
A contagem é do agente; no prazo, `Shutdown`/`Restart` rodam `shutdown /t 0` (com `/f` só se `force`), ver spec 005.

### Tela (specs 017–019)
| Método | Retorno | Permissão |
|---|---|---|
| `ListMonitors()` | `MonitorInfo[]` | `screen.view` |
| `StartScreenStream(ScreenStreamOptions options)` | `ScreenStreamInfo` (URL do `/ws/screen`, modo) | `screen.view` |
| `UpdateScreenStream(ScreenStreamOptions options)` **[novo]** | — | `screen.view` |
| `StopScreenStream()` | — | `screen.view` |
| `RequestScreenControl()` / `ReleaseScreenControl()` **[novo]** | `ScreenControlState` | `screen.control` |
| `SendInput(InputEvent[] batch)` | — | `screen.control` (e ser o controlador atual) |
| `WebRtcSignal(WebRtcSignalDto payload)` | — | `screen.view` |

### Processos (spec 021)
| Método | Retorno | Permissão |
|---|---|---|
| `ListProcesses(ProcessQuery query)` | `ProcessInfo[]` | `processes.view` |
| `GetProcessDetails(int pid)` **[novo]** | `ProcessDetails` | `processes.view` |
| `KillProcess(int pid, bool tree, bool localConfirmation)` | — | `processes.kill` |
| `ListWindows()` | `WindowInfo[]` | `processes.view` |
| `FocusWindow(long hwnd)` | — | `processes.kill` |

### Favoritos (spec 022)
| Método | Retorno | Permissão |
|---|---|---|
| `ListFavorites()` | `FavoriteDto[]` | `favorites.open` |
| `OpenFavorite(Guid id)` | — | `favorites.open` |
| `OpenUrl(string url)` | — | `favorites.open` (só `http`/`https`) |
| `ProposeFavorite(FavoriteProposal dto)` | `Guid` (pendente até confirmar no PC) | `favorites.manage` |
| `DeleteFavorite(Guid id)` **[novo]** | — | `favorites.manage` |

### Agendador (spec 023)
| Método | Retorno | Permissão |
|---|---|---|
| `ListSchedules()` | `ScheduleDto[]` | `schedules.manage` |
| `UpsertSchedule(ScheduleDto dto)` | `ScheduleDto` (com `nextRunAt`) | `schedules.manage` (+ permissão da ação agendada) |
| `DeleteSchedule(Guid id)` | — | `schedules.manage` |
| `RunScheduleNow(Guid id)` | — | `schedules.manage` (+ permissão da ação) |
| `CancelScheduleRun(Guid id)` **[novo]** | — | `schedules.manage` (cancela durante o aviso antecipado) |

### Claude Code (specs 008–010, 013, 016)
Nomes com "Claude" para não colidir com outras áreas (como no prompt original).

| Método | Retorno | Permissão |
|---|---|---|
| `ListClaudeProjects()` | `ClaudeProjectDto[]` | `claude.view` |
| `ListClaudeSessions(ClaudeSessionFilter filter)` | `ClaudeSessionDto[]` (gerenciadas, externas e recentes do histórico) | `claude.view` |
| `StartClaudeSession(Guid projectId, ClaudeSessionOptions options)` | `ClaudeSessionDto` | `claude.chat` |
| `ResumeClaudeSession(Guid sessionId, ResumeOptions options)` | `ClaudeSessionDto` (`options.fork` → `--fork-session`) | `claude.chat` |
| `SendClaudeMessage(Guid sessionId, ClaudeContentBlock[] content)` | `long` (seq da mensagem) | `claude.chat` |
| `InterruptClaude(Guid sessionId)` | — | `claude.chat` |
| `EndClaudeSession(Guid sessionId)` | — | `claude.chat` |
| `GetClaudeSessionEvents(Guid sessionId, long afterSeq, int limit)` | `ClaudeEventDto[]` | `claude.view` |
| `OpenClaudeSession(Guid sessionId)` / `CloseClaudeSession(Guid sessionId)` **[novo]** | — | `claude.view` (entra/sai do grupo `claude-session:{id}`) |
| `ListPendingPermissions()` **[novo]** | `PermissionRequestDto[]` | `claude.view` |
| `RespondPermission(Guid requestId, PermissionDecision decision, PermissionScope scope, string? message, bool localConfirmation)` | — | `claude.approve` |
| `StartNativeRemoteControl(Guid projectId)` | `RemoteControlInfo` (link/QR) | `claude.chat` |
| `GetClaudeUsage(UsageQuery query)` **[novo]** | `ClaudeUsageDto` (por sessão, dia, projeto) | `claude.view` |
| `ListQuickCommands(Guid? projectId)` | `QuickCommandDto[]` | `claude.view` |
| `UpsertQuickCommand(QuickCommandDto dto)` / `DeleteQuickCommand(Guid id)` **[novo]** | — | `claude.chat` |

`ClaudeContentBlock`: `{ type: "text", text }` ou `{ type: "image", mediaType, dataBase64 }` (lado maior ≤ 1568 px),
convertido pelo agente para o formato do stream-json (ADR-0004).

### Git (spec 012)
| Método | Retorno | Permissão |
|---|---|---|
| `GitStatus(Guid projectId)` | `GitStatusDto` (branch, ahead/behind, arquivos) | `dev.git.read` |
| `GitDiff(Guid projectId, string? path, bool staged)` | `GitDiffDto` (com limite e marcação de binários) | `dev.git.read` |
| `GitLog(Guid projectId, int limit)` | `GitCommitDto[]` | `dev.git.read` |
| `GitListBranches(Guid projectId)` **[novo]** | `GitBranchDto[]` | `dev.git.read` |
| `GitGenerateCommitMessage(Guid projectId)` | `string` (Conventional Commits) | `dev.git.write` |
| `GitCommit(Guid projectId, string message, string[]? paths)` | `GitCommitDto` | `dev.git.write` |
| `GitPush(Guid projectId)` / `GitPull(Guid projectId)` | `GitOperationResult` | `dev.git.write` |
| `GitCreateBranch(Guid projectId, string name, bool checkout)` | — | `dev.git.write` |
| `GitCheckout(Guid projectId, string branch)` **[novo]** | — | `dev.git.write` |
| `GitDiscard(Guid projectId, string[]? paths, bool localConfirmation)` | — | `dev.git.discard` |

### Builds e testes (spec 014)
| Método | Retorno | Permissão |
|---|---|---|
| `ListBuildProfiles(Guid projectId)` | `BuildProfileDto[]` | `dev.builds` |
| `ListBuildRuns(Guid projectId, int limit)` **[novo]** | `BuildRunDto[]` | `dev.builds` |
| `RunBuild(Guid profileId)` | `BuildRunDto` | `dev.builds` |
| `CancelBuild(Guid runId)` | — | `dev.builds` |
| `AskClaudeToFix(Guid runId, Guid? sessionId)` | `ClaudeSessionDto` | `dev.builds` + `claude.chat` |

Perfis de build não são criados pelo app (executável arbitrário): só por detecção automática ou na bandeja.

### Preview (spec 015)
| Método | Retorno | Permissão |
|---|---|---|
| `ListListeningPorts()` | `ListeningPortDto[]` (porta, endereço, processo) | `dev.preview` |
| `CreatePreviewTicket(int port)` **[novo]** | `PreviewTicketDto` (URL com ticket de uso único) | `dev.preview` |

### Monitoramento e alertas (spec 024)
| Método | Retorno | Permissão |
|---|---|---|
| `ListAlertRules()` | `AlertRuleDto[]` | `monitor.view` |
| `UpsertAlertRule(AlertRuleDto dto)` / `DeleteAlertRule(Guid id)` **[novo]** | — | `alerts.manage` |
| `ListAlerts(bool activeOnly)` **[novo]** | `AlertEventDto[]` | `monitor.view` |
| `SilenceAlert(Guid alertEventId, int minutes)` **[novo]** | — | `alerts.manage` |

### Segurança (specs 003, 026)
| Método | Retorno | Permissão |
|---|---|---|
| `ListDevices()` | `DeviceDto[]` (com conectado agora) | `security.manageDevices` |
| `UpdateDevicePermissions(Guid id, DevicePermissionsDto dto)` | — | `security.manageDevices` (`security.lastAdmin` protegido) |
| `Panic()` | — | `security.panic` |

### Push (spec 011)
| Método | Retorno | Permissão |
|---|---|---|
| `RegisterPushToken(PushProvider provider, string token)` | — | autenticado |
| `UnregisterPushToken()` **[novo]** | — | autenticado |
| `GetNotificationPreferences()` / `UpdateNotificationPreferences(NotificationPreferencesDto dto)` **[novo]** | — | autenticado |

## 4. Hub — servidor → cliente (`IRemoteClient`)

| Evento | Payload | Destino | Spec |
|---|---|---|---|
| `DashboardDelta` | `(long version, CardPatch[] patches)` | `dashboard` (filtrado por permissão) | 007/028 |
| `StatusUpdated` | `SystemStatus` | `perm:status.view` | 007 |
| `MetricsTick` | `MetricsSnapshot` | inscritos em métricas | 007 |
| `PowerCountdown` / `PowerCancelled` | `PowerCountdownDto` / `PowerCancelledDto` | todos | 005 |
| `ScreenViewersChanged` **[novo]** | `ScreenViewersDto` (quem assiste, quem controla) | `perm:screen.view` | 018 |
| `WebRtcSignal` | `WebRtcSignalDto` | conexão solicitante | 019 |
| `ClaudeEvent` | `(Guid sessionId, long seq, JsonElement evt)` | `claude-session:{id}` | 008 |
| `ClaudeSessionStateChanged` | `ClaudeSessionStateDto` | `perm:claude.view` | 008 |
| `ClaudePermissionRequested` | `PermissionRequestDto` | `perm:claude.view` (botões só para `claude.approve`) | 009 |
| `ClaudePermissionResolved` | `PermissionResolvedDto` (decisão, por quem) | `perm:claude.view` | 009 |
| `BuildOutput` / `BuildFinished` | `(Guid runId, string chunk)` / `BuildRunDto` | `perm:dev.builds` | 014 |
| `ScheduleCountdown` / `ScheduleFired` | `ScheduleCountdownDto` / `ScheduleRunDto` | `perm:schedules.manage` | 023 |
| `AlertRaised` / `AlertResolved` | `AlertEventDto` | `perm:monitor.view` | 024 |
| `FavoriteProposalResolved` **[novo]** | `(Guid id, bool accepted)` | aparelho que propôs | 022 |
| `AuditAppended` | `AuditEntryDto` | `perm:security.viewAudit` | 003 |
| `DevicePermissionsChanged` **[novo]** | `DevicePermissionsDto` | `device:{id}` | 003 |
| `DeviceRevoked` | `(Guid deviceId)` | `device:{id}` (e depois desconecta) | 002 |
| `PanicActivated` | `PanicDto` | todos (e depois desconecta) | 026 |
| `AgentShuttingDown` | `(string reason)` | todos | 001 |

`ScreenFrame` saiu do Hub: os quadros MJPEG vão pelo `/ws/screen` binário (ADR-0005).

## 5. DTOs principais

```csharp
public sealed record HubResponse<T>(bool Ok, T? Data, HubError? Error);
public sealed record HubError(string Code, string Message, JsonElement? Details);

public sealed record DashboardOptions(int MetricsIntervalMs, bool IncludeScreenThumbnail, bool LowDataMode);
public sealed record DashboardSnapshot(long Version, DateTimeOffset At, IReadOnlyList<CardState> Cards);
public sealed record CardState(string CardId, string Kind, JsonElement Data, IReadOnlyList<string> AvailableActions);
public sealed record CardPatch(string CardId, CardState? Card); // [novo] troca o card inteiro (com as ações); nulo = removido
// Cards do painel v1 (spec 007): pc, power, cpu, memory, network, disks, gpu, battery (só com bateria).
// Dados: PcCardData, PowerCardData, CpuMetrics, MemoryMetrics, NetworkMetrics, DisksCardData, GpuMetrics, BatteryStatusDto.
// Ações do card de energia: power.lock|sleep|restart|hibernate|shutdown|cancel (só as permitidas e disponíveis).

public enum PowerActionKind { Shutdown, Restart, Sleep, Hibernate, Lock }
public sealed record PowerActionRequest(PowerActionKind Action, int DelaySeconds, bool Force,
    bool AcknowledgeActiveWork, bool LocalConfirmation);
public sealed record PowerCountdownDto(PowerActionKind Action, DateTimeOffset ExecutesAt, bool Force,
    Guid? RequestedByDeviceId, string? RequestedByDeviceName, bool UpdatePending); // [novo] Force, nome, UpdatePending
public enum PowerCancelSource { Device, Tray, AgentShutdown }
public sealed record PowerCancelledDto(PowerActionKind Action, Guid? CancelledByDeviceId, PowerCancelSource Source);

public sealed record ClaudeSessionOptions(string? Model, string PermissionMode, string? Title);
public sealed record ResumeOptions(bool Fork, string? Model, string? PermissionMode);
public sealed record ClaudeContentBlock(string Type, string? Text, string? MediaType, string? DataBase64);
public sealed record PermissionRequestDto(Guid Id, Guid SessionId, string ProjectName, string ToolName,
    JsonElement Input, string? Reason, DateTimeOffset ExpiresAt);
public enum PermissionDecision { Allow, Deny }
public enum PermissionScope { Once, Session }

public sealed record InputEvent(string Type, double? X, double? Y, int? Button, int? Delta,
    string? Text, string[]? Keys);   // Type: move, down, up, click, doubleClick, scroll, text, keys
```

## 6. Catálogo de códigos de erro

| Código | HTTP | Quando |
|---|---|---|
| `auth.unauthorized` | 401 | Token ausente ou inválido |
| `auth.revoked` | 401 | Aparelho revogado |
| `auth.forbidden` | 403 | Sem a permissão do método |
| `agent.paused` | 503 | Acesso remoto pausado (bandeja ou pânico) |
| `rate.limited` | 429 | Limite de requisições |
| `validation.failed` | 400 | Entrada inválida (detalhes por campo) |
| `pairing.invalidCode` | 400 | Código de pareamento inexistente (conta como tentativa inválida) |
| `pairing.expired` / `pairing.consumed` | 410 | Código expirado / já usado |
| `pairing.rejected` | 403 | Pareamento recusado na bandeja |
| `pairing.confirmationTimeout` | 408 | Sem confirmação na bandeja em 60 s |
| `pairing.locked` | 429 | 5 falhas em 10 min |
| `security.lastAdmin` | 409 | Remover/rebaixar o último Administrador |
| `devices.notFound` | 404 | Aparelho inexistente |
| `power.alreadyScheduled` | 409 | Já há ação de energia agendada |
| `power.activeWork` | 409 | Sessões do Claude ou builds ativos sem confirmação extra |
| `power.hibernateUnavailable` | 409 | Hibernação desativada |
| `power.sleepUnavailable` **[novo]** | 409 | Nenhum estado de suspensão disponível (S1–S3) |
| `power.nothingToCancel` | 404 | Nenhuma ação pendente |
| `screen.secureDesktop` | 409 | Tela de bloqueio/UAC |
| `screen.monitorNotFound` | 404 | Monitor inexistente |
| `screen.controlTaken` | 409 | Outro aparelho está controlando |
| `processes.protected` / `processes.notFound` | 403 / 404 | Processo protegido / inexistente |
| `favorites.invalidUrl` / `favorites.notFound` | 400 / 404 | URL não `http/https` / favorito inexistente |
| `files.outsideRoot` | 403 | Path traversal, link/junction para fora, ADS, nome reservado |
| `files.notFound` / `files.inUse` / `files.tooLarge` / `files.invalidName` / `files.rootUnavailable` | 404 / 409 / 413 / 400 / 404 | |
| `claude.cliNotFound` / `claude.notAuthenticated` | 424 | CLI ausente / sem login |
| `claude.projectNotAllowed` | 403 | Diretório fora da whitelist |
| `claude.sessionLimit` | 409 | Limite de sessões simultâneas (padrão 3) |
| `claude.sessionNotFound` | 404 | |
| `claude.permissionResolved` | 409 | Pedido já decidido por outro aparelho |
| `git.notARepository` / `git.conflict` / `git.pushRejected` / `git.authRequired` / `git.operationInProgress` | 400 / 409 / 409 / 424 / 409 | |
| `builds.alreadyRunning` | 409 | |
| `preview.portNotAllowed` | 403 | Porta do agente ou não local |
| `schedules.invalidTrigger` | 400 | |
| `internal.error` | 500 | Erro inesperado (sem detalhes) |
