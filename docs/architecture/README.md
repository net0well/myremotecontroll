# Arquitetura — MyRemoteControll

> **Status:** Aprovado em 2026-10-18. Evolui pelas specs (mudanças passam pelo `/spec`). Depende dos ADRs aceitos em `docs/adr/`.
> Documentos: [modelo de dados](data-model.md) · [contrato da API e do Hub](hub-contract.md)

## 1. Visão geral

```
Celular (Flutter)                          PC Windows
┌──────────────────────┐   HTTPS/WSS   ┌───────────────────────────────────────────┐
│ MyRemoteControll App │──────────────►│ MyRemoteControll Agent (sessão do usuário) │
│ UI → Lógica → Dados  │ LAN/Tailscale │  :47800  REST /api/v1 · Hub /hubs/remote   │
└──────────▲───────────┘               │          WS /ws/screen · YARP /preview     │
           │ push (só tipo + IDs)      │  127.0.0.1:47801  MCP /mcp · /hooks/claude │
     FCM / ntfy ◄──────────────────────│  SQLite · Serilog · bandeja                │
                                       │      │ named pipe (somente leitura)        │
                                       │      ▼                                     │
                                       │ MyRemoteControll Sensors (serviço elevado) │
                                       │ processos `claude` · `git` · builds        │
                                       └───────────────────────────────────────────┘
```

Decisões que sustentam este desenho: ADR-0002 (Vertical Slice), 0003 (bandeja), 0004 (Claude Code), 0005 (SignalR +
REST), 0006 (TLS), 0014 (sensores), 0021 (painel).

## 2. Estrutura do repositório

```
myremotecontroll/
├── CLAUDE.md                       # regras gerais (seção 14 do documento base)
├── .claude/
│   ├── rules/                      # 10 regras do dotnet-claude-kit + project.md
│   └── settings.json
├── docs/
│   ├── adr/                        # 0001–0021
│   ├── specs/                      # NNN-slug.md (gerados pelo /spec)
│   ├── architecture/               # este documento, modelo de dados, contrato
│   └── base/                       # documento base v2.2 + prompt original
├── agent/                          # .NET 10 (criado pelo /dotnet-init)
├── app/                            # Flutter (criado na spec 004)
└── tools/claude-hooks/             # exemplos de hooks para sessões externas
```

### 2.1 Agente (`agent/`)

```
agent/
├── CLAUDE.md                                  # gerado pelo /dotnet-init
├── MyRemoteControll.slnx
├── Directory.Build.props                      # net10.0, C# 14, nullable, warnings as errors
├── Directory.Packages.props                   # Central Package Management
├── src/
│   ├── MyRemoteControll.Agent/                # net10.0-windows (WinForms + ASP.NET Core)
│   │   ├── Program.cs                         # bootstrap Serilog, instância única, host + bandeja
│   │   ├── Hosting/                           # Kestrel (2 listeners), Options, pausa de acesso remoto
│   │   ├── Tray/                              # NotifyIcon, menus, QR de pareamento, diálogos de confirmação
│   │   ├── Hubs/                              # RemoteHub (partial por área), filtros de permissão/auditoria
│   │   │   ├── RemoteHub.cs
│   │   │   ├── RemoteHub.Power.cs             # métodos finos que delegam aos handlers da feature
│   │   │   └── ...
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/                   # AgentDbContext, configurações EF, migrations
│   │   │   ├── Auth/                          # DeviceTokenAuthenticationHandler, policies perm:*
│   │   │   ├── Audit/                         # AuditWriter (Channel + lote)
│   │   │   └── Events/                        # barramento interno de eventos (Channel<T>)
│   │   └── Features/                          # uma pasta por área (vertical slices)
│   │       ├── Pairing/  Devices/  Audit/  Power/  Status/  Dashboard/
│   │       ├── Claude/  Git/  Builds/  Preview/  QuickCommands/  Push/
│   │       ├── Screen/  Files/  Processes/  Favorites/  Scheduler/
│   │       └── Monitoring/  Alerts/  Panic/
│   │           # cada pasta: <Operação>.cs (request + validator + handler),
│   │           # <Area>Endpoints.cs (REST, se houver), <Area>Card.cs (publicação no painel)
│   ├── MyRemoteControll.Windows/              # interop atrás de interfaces
│   │   ├── Power/  Input/  Capture/  Toasts/  Processes/  Windows/  Network/  Shell/
│   ├── MyRemoteControll.Claude/
│   │   ├── Cli/                               # IClaudeCli, localização do executável, ClaudeProcess
│   │   ├── StreamJson/                        # StreamJsonParser, modelos tolerantes
│   │   ├── Permissions/                       # servidor MCP (ferramenta approve)
│   │   └── Hooks/                             # modelos de payload, instalador/mesclador de settings.json
│   ├── MyRemoteControll.Sensors/              # Worker Service elevado + named pipe
│   └── MyRemoteControll.Contracts/
│       ├── Hub/                               # IRemoteHub, IRemoteClient, HubResponse<T>
│       ├── Dtos/<Area>/
│       ├── Errors/ErrorCodes.cs
│       └── Security/PermissionKeys.cs, Roles.cs
├── tests/
│   ├── MyRemoteControll.Agent.Tests/          # unidade (handlers, parser, validação de caminhos, agendador)
│   └── MyRemoteControll.Agent.IntegrationTests/  # WebApplicationFactory + SQLite temporário + cliente SignalR
└── installer/                                 # Inno Setup + scripts de publicação
```

Os métodos do Hub ficam em `Hubs/` (arquivos `partial` por área) porque uma classe `partial` exige o mesmo namespace;
eles só validam e delegam aos handlers em `Features/<Area>/`.

### 2.2 App (`app/`)

```
app/
├── CLAUDE.md
├── pubspec.yaml                     # name: my_remote_controll
├── l10n.yaml
├── l10n/app_pt.arb                  # textos pt-BR
├── lib/
│   ├── main.dart
│   ├── app/
│   │   ├── app.dart                 # MaterialApp.router, tema, localização
│   │   ├── router/                  # rotas go_router (shell com seletor de PC)
│   │   └── theme/                   # tokens: cores, tipografia, espaçamentos, raios, elevação, movimento
│   ├── core/
│   │   ├── network/                 # ConnectionManager por PC, cliente Hub, cliente REST (dio), pinning SPKI
│   │   ├── storage/                 # PCs pareados, tokens (flutter_secure_storage), layout do painel
│   │   ├── security/                # bloqueio por biometria/PIN, confirmação de ações destrutivas
│   │   ├── errors/                  # AppError, mapeamento de ProblemDetails/HubResponse
│   │   ├── logging/
│   │   └── widgets/                 # design system: DashCard, Gauge, Sparkline, LiveBadge, SlideToConfirm...
│   └── features/
│       ├── pairing/ dashboard/ power/ screen/ schedules/ processes/ apps/ files/
│       ├── claude/ git/ builds/ preview/ monitoring/ security/ settings/
│       │   # cada feature: ui/ (telas e widgets), logic/ (notifiers/viewmodels),
│       │   # data/ (models, repositories, services)
├── test/
└── integration_test/
```

## 3. Fluxos principais

### 3.1 Conexão

1. App lê o PC ativo (hosts, porta, pin SPKI, token) e tenta os hosts em paralelo (LAN, IP Tailscale, MagicDNS).
2. TLS validado pelo pin; `GET /api/v1/info`; conexão ao Hub com `Authorization: Bearer` (negociação) e
   `access_token` (WebSocket).
3. `SubscribeDashboard` → `DashboardSnapshot`; depois `DashboardDelta`.
4. Queda: reconexão exponencial; ao voltar, novo snapshot e, no chat aberto, `GetClaudeSessionEvents(afterSeq)`.

### 3.2 Permissão do Claude (sessão gerenciada)

1. `claude` chama `mcp__myremotecontroll__approve` no loopback.
2. Agente grava `PermissionRequest`, emite `ClaudePermissionRequested` (grupo `perm:claude.approve`) e push.
3. Primeiro `RespondPermission` vence; os demais aparelhos recebem `ClaudePermissionResolved`.
4. Sem resposta até `ExpiresAt` → `deny` com "Tempo esgotado".

### 3.3 Ação de energia

1. App confirma (deslizar + biometria; aviso de que o PC só liga presencialmente).
2. `PowerAction(Shutdown, 60, false, localConfirmation: true)` → agente verifica sessões do Claude e builds ativos; se
   houver e o pedido não trouxer `acknowledgeActiveWork`, devolve `power.activeWork` com a lista para o app pedir
   confirmação extra.
3. `shutdown /s /t 60`, toast no PC, `PowerCountdown` para todos; `CancelPowerAction` → `shutdown /a` + `PowerCancelled`.

## 4. Divergências entre os documentos de entrada (resolvidas a favor do documento base v2.2)

| Tema | Prompt original | Documento base v2.2 (adotado) |
|---|---|---|
| Runtime | .NET 8 | .NET 10 LTS (o .NET 8 perde suporte em 10/11/2026) |
| Projetos | Core, Claude, Screen, Infrastructure, Contracts | Agent (features), Windows, Claude, Sensors, Contracts |
| Energia | Sem hibernar | Inclui hibernar; "sair da conta" fica fora (spec 005) |
| Sensores | No agente | Serviço elevado separado |
| Documentação do Claude Code | docs.claude.com | code.claude.com/docs |
| Fases | 8 fases | Fases 0–7 com specs numeradas |
