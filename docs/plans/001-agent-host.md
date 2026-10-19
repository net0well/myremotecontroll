# Plano: Spec 001 — Host do agente

**Spec:** [001-agent-host](../specs/001-agent-host.md) (Approved) · **Arquitetura:** Vertical Slice (ADR-0002)
**Projetos afetados:** `Agent`, `Windows`, `Contracts`, `Agent.Tests`, `Agent.IntegrationTests`

## Decisões de implementação

- **Identificação do listener:** middleware de conexão do Kestrel (`ListenOptions.Use`) grava o tipo do listener
  (`Public`/`Loopback`) em `IConnectionItemsFeature`. Endpoints marcados com `.LoopbackOnly()` respondem 404 fora do
  loopback; os demais respondem 404 no loopback. Não depende do header `Host` (que o cliente controla).
- **Ciclo de vida:** `AgentRuntime` constrói e inicia o `WebApplication` fora da thread da UI, expõe o estado
  (`Starting`, `Running`, `Paused`, `Faulted`) para a bandeja e refaz a tentativa a cada 30 s quando a porta está
  ocupada (`TimeProvider`).
- **Banco:** `DatabaseInitializer` roda antes da configuração do host: verifica integridade, preserva arquivo
  corrompido, faz backup (API de backup do SQLite) antes de migrations pendentes, restaura em caso de falha.
  `AgentSettings` vira fonte de configuração própria (`AgentSettingsConfigurationSource`) com recarga.
- **Certificado:** `ICertificateProvider`; produção em `CurrentUser\My` (ECDSA P-256, 5 anos, chave não exportável,
  impressão digital guardada em `AgentSettings`); testes com provedor efêmero.
- **Autostart:** `schtasks /Create /XML` com `LogonTrigger` do usuário atual e `LeastPrivilege` (verificado: funciona sem
  elevação); estado lido por `/Query` (código de saída, nunca o texto localizado).
- **Hub:** `RemoteHub : Hub<IRemoteClient>` com `RequireAuthorization` e esquema `DeviceToken` provisório (sempre
  `NoResult` → 401) até a spec 002. `AgentShuttingDown` enviado em `IHostedLifecycleService.StoppingAsync`.
- **Logs:** Serilog em dois estágios; `Microsoft.AspNetCore` em `Warning` para que URLs com query string (ex.:
  `access_token`) nunca sejam registradas; correlation ID por requisição.
- **Testes de integração** sobem o Kestrel real em portas efêmeras (não `TestServer`), porque os ACs dependem dos dois listeners.

## Passos

1. `Contracts`: `ErrorCodes`, `AgentInfoResponse`, `IRemoteClient`, `IRemoteHub`.
2. `Windows`: `IProcessRunner`, `ScheduledTaskAutostart`.
3. `Agent/Infrastructure`: opções + validação, caminhos, `AgentDbContext` + migration inicial, `DatabaseInitializer`,
   fonte de configuração `AgentSettings`, certificado, Serilog + correlation ID, `IEndpointGroup` + `MapEndpoints`,
   roteamento por listener, porta de pausa, ProblemDetails.
4. `Agent/Features`: `Info` (`GET /api/v1/info`), `Health` (`/health` só loopback).
5. `Agent/Hubs`: `RemoteHub`, notificador de encerramento.
6. `Agent/Hosting`: `AgentRuntime`, `SingleInstanceGuard`.
7. `Agent/Tray`: ícone, menu, avisos.
8. `Program.cs`: instância única → bandeja → runtime.

## Critérios de aceite → testes

| AC | Teste | Tipo |
|---|---|---|
| AC-1 | `AC1_AutostartTask_UsesLogonTriggerForCurrentUserWithoutElevation` + verificação manual no logon | unidade + manual |
| AC-2 | `AC2_SecondInstance_IsRejectedAndSignalsFirstInstance` | unidade |
| AC-3 | `AC3_MissingDatabase_IsCreatedByMigrations` | integração |
| AC-4 | `AC4_Health_Returns200OnLoopbackAnd404OnPublicListener` | integração |
| AC-5 | `AC5_PausedRemoteAccess_Returns503UntilResumed` | integração |
| AC-6 | `AC6_Info_WithoutToken_ReturnsOnlyApiVersionAndRequiresPairing` | integração |
| AC-7 | `AC7_CorruptDatabase_IsPreservedAndRecreated` | integração |
| AC-8 | `AC8_Stopping_BroadcastsAgentShuttingDown` | unidade |
| AC-9 | `AC9_DisablingAutostart_DeletesTheTask` | unidade |
| AC-10 | `AC10_Certificate_IsEcdsaP256NonExportableValidForFiveYears` + `AC10_PublicListener_RejectsPlainHttp` | unidade + integração |
| AC-11 | `AC11_PortInUse_FaultsWithPortAndRetriesAfter30Seconds` | integração |
| AC-12 | `AC12_PausedRemoteAccess_KeepsLoopbackWorking` | integração |

## Riscos

- `SslStream` no Windows recusa chave efêmera: o certificado passa por PKCS#12 antes de ir ao Kestrel.
- Teste do certificado real toca o repositório do usuário: usa nome exclusivo e remove o certificado ao final.

## Resultado (2026-10-18)

- `dotnet build`: 0 avisos, 0 erros. `dotnet format --verify-no-changes`: ok.
- `dotnet test`: 28 aprovados; o teste de sistema explícito (tarefa real no Agendador, sem elevação) também aprovado.
- Teste de fumaça com o executável real: `/health` 200 no loopback e 404 no público; `/api/v1/info` só com
  `apiVersion` e `requiresPairing`; segunda instância sai com código 0.
- **Não funcionais** (Release, self-contained, ReadyToRun, 15 s ocioso):

  | Métrica | Medido | Meta |
  |---|---|---|
  | Inicialização até responder (cache quente) | 1,8 s | < 3 s |
  | Working set privado (coluna "Memória" do Gerenciador de Tarefas) | 21 MB | < 80 MB |
  | Bytes privados | 28 MB | — |
  | Working set total (inclui DLLs do .NET compartilhadas) | 105 MB | — |
  | CPU ocioso | ≈ 0,8% de um núcleo | < 1% |

  Em build Debug a inicialização fica entre 2,2 e 3,0 s; a primeira execução de arquivos recém-publicados (cache frio)
  passa de 4 s. A spec 027 publica com ReadyToRun.
- **Pendente de verificação manual:** AC-1 (ícone em até 10 s após o logon) exige reiniciar a sessão do Windows.
