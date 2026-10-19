# MyRemoteControll — Documento base para Specs, ADRs e Fases

> **Versão:** 2.2 (painel em tempo real; Wake-on-LAN removido do escopo) · **Data:** 18/10/2026 · **Idioma do projeto:** português do Brasil
> **Uso:** este documento é a **fonte de verdade inicial** para gerar as especificações com o comando `/spec` do **dotnet-claude-kit**, decidir a arquitetura com **ADRs** e executar o desenvolvimento em **fases**. Ele não substitui o `/spec`: ele alimenta o `/spec` com respostas prévias, para que o questionamento seja mais rápido e mais preciso.

---

## Sumário

0. [Regras obrigatórias para a IA](#0-regras-obrigatórias-para-a-ia)
1. [Fluxo de trabalho: ADRs → /spec → /plan → implementação → /verify](#1-fluxo-de-trabalho)
2. [Visão do produto](#2-visão-do-produto)
3. [Stack e versões de referência (pesquisado em out/2026)](#3-stack-e-versões-de-referência)
4. [Arquitetura geral](#4-arquitetura-geral)
5. [Estrutura do repositório](#5-estrutura-do-repositório)
6. [ADRs propostos](#6-adrs-propostos)
7. [Modelo de domínio e dados](#7-modelo-de-domínio-e-dados)
8. [Contratos de API (REST + SignalR)](#8-contratos-de-api)
9. [Autorização: papéis e permissões](#9-autorização-papéis-e-permissões)
10. [Backlog de specs por fase](#10-backlog-de-specs-por-fase)
11. [Requisitos não funcionais globais](#11-requisitos-não-funcionais-globais)
12. [Riscos, limitações e pontos a verificar](#12-riscos-limitações-e-pontos-a-verificar)
13. [Convenções de código, testes e commits](#13-convenções)
14. [Trecho sugerido para o CLAUDE.md](#14-trecho-sugerido-para-o-claudemd)
15. [Referências](#15-referências)

---

## 0. Regras obrigatórias para a IA

Estas regras valem para **toda** sessão de desenvolvimento do MyRemoteControll:

1. **.NET / C#: usar SEMPRE o dotnet-claude-kit.**
   - Todo trabalho no agente (C#/.NET) deve usar as skills, agentes, comandos, regras e o servidor MCP Roslyn do plugin **dotnet-claude-kit** (`codewithmukesh/dotnet-claude-kit`).
   - Fluxo obrigatório por funcionalidade: `/spec` → `/plan` → `/tdd` ou `/scaffold` → `/verify` → `/code-review`. Usar `/build-fix` para erros de compilação, `/security-scan` ao fim de cada fase, `/arch-check` e `/outdated` periodicamente, `/checkpoint` antes de mudanças arriscadas e `/wrap-up` ao encerrar a sessão.
   - Carregar as skills de conhecimento pertinentes (ex.: `modern-csharp`, `minimal-api`, `ef-core`, `authentication`, `error-handling`, `serilog`, `testing`, `configuration`, `dependency-injection`, `resilience`) antes de escrever código.
   - Preferir as ferramentas MCP do Roslyn (`find_symbol`, `get_file_outline`, `get_symbol_source`, `get_endpoint_map`, `get_di_registrations`, `get_diagnostics`, `detect_antipatterns` etc.) em vez de ler arquivos inteiros.
   - Copiar as 10 regras do kit para `.claude/rules/` do repositório para que fiquem sempre carregadas.
2. **Flutter / Dart: usar SEMPRE as skills de Flutter instaladas.**
   - Plugin oficial **dart-flutter** (`flutter/agent-plugins`) e skills Dart (`dart-lang/skills`), incluindo o servidor MCP do Dart e Flutter.
   - Skills a usar conforme o caso: `flutter-apply-architecture-best-practices`, `flutter-setup-declarative-routing`, `flutter-setup-localization`, `flutter-implement-json-serialization`, `flutter-use-http-package`, `flutter-build-responsive-layout`, `flutter-fix-layout-issues`, `flutter-add-widget-test`, `flutter-add-integration-test`, `flutter-add-widget-preview`, além de quaisquer outras skills de Flutter instaladas na máquina.
   - Antes de cada tarefa no app, listar as skills disponíveis e carregar as que se aplicam.
3. **Specs antes de código.** Nenhuma funcionalidade "spec-worthy" é implementada sem spec com status **Approved** em `docs/specs/`. Código nunca diverge silenciosamente de uma spec aprovada: se algo mudar, a spec volta para **In Review**.
4. **ADRs antes de specs.** As decisões arquiteturais da seção 6 devem ser registradas em `docs/adr/` e aceitas antes de iniciar as specs que dependem delas.
5. **Nunca assumir.** O conteúdo deste documento são **respostas prévias**. Durante o `/spec`, a IA deve validar cada uma comigo e perguntar sobre tudo o que estiver ausente, ambíguo ou contraditório.
6. **Nunca usar versões de memória.** Verificar sempre a versão estável mais recente de cada pacote NuGet e pub.dev antes de adicioná-lo (regra `packages` do kit) e conferir licenças.
7. **Documentação oficial primeiro.** Para Claude Code (flags, stream-json, hooks, MCP, permissões), consultar a documentação atual em `https://code.claude.com/docs` antes de implementar. Não inventar flags.
8. **Idioma.** Specs, ADRs, comentários de código e textos de interface em português do Brasil. Identificadores de código em inglês.

---

## 1. Fluxo de trabalho

### 1.1 Preparação do ambiente (uma vez)

```bash
# .NET
dotnet --version                      # deve ser SDK 10.x
dotnet tool install -g CWM.RoslynNavigator

# Dentro do Claude Code
/plugin marketplace add codewithmukesh/dotnet-claude-kit
/plugin install dotnet-claude-kit

# Flutter (plugin oficial com skills + MCP)
claude plugin marketplace add flutter/agent-plugins
claude plugin install dart-flutter@dart-flutter
# Skills Dart (opcional, recomendado)
npx skills add dart-lang/skills --skill '*' --agent universal
```

### 1.2 Ordem de execução

1. **Inicializar o repositório** (seção 5) e rodar `/dotnet-init` dentro de `agent/` (modo greenfield). Responder ao questionário de arquitetura com as respostas da seção 6 (ADR-0002).
2. **Registrar os ADRs** da seção 6 em `docs/adr/NNNN-titulo.md` (formato da seção 6.1), revisar e marcar como **Aceito**.
3. **Para cada spec do backlog (seção 10), na ordem das fases:**
   1. `/spec <ID e título>` — usar o bloco da spec como ponto de partida; o comando faz as rodadas de perguntas nas 9 dimensões, grava `docs/specs/NNN-slug.md` e só segue com aprovação explícita.
   2. `/plan` — plano de implementação a partir da spec aprovada (lado .NET). Para a parte Flutter, gerar o plano com as skills de Flutter, mapeando cada AC para telas, providers e testes.
   3. `/tdd` (ACs viram testes) ou `/scaffold` (fatias do agente).
   4. Implementar o lado Flutter com as skills de Flutter.
   5. `/verify` (pipeline de 7 fases) + `flutter analyze` + `flutter test`.
   6. `/code-review` e commit referenciando a spec: `feat(power): ações de energia (spec 005)`.
4. **Ao fim de cada fase:** `/security-scan`, `/arch-check`, `/outdated`, revisão manual dos ACs e tag `fase-N`.

### 1.3 Como o `/spec` do kit funciona (resumo para alinhar expectativas)

- Reformula a ideia e pede confirmação antes de perguntar.
- Faz rodadas de 3 a 5 perguntas, nas 9 dimensões: **Problema e usuários, Escopo, Domínio e dados, Contrato de API, Autorização, Casos de borda e falhas, Requisitos não funcionais, Integrações, Critérios de aceite (Given/When/Then)**.
- "Não sei" vira **Decisão adiada** com fallback explícito.
- Grava em `docs/specs/<NNN>-<slug>.md` com status **Draft → In Review → Approved**; "Open Questions" precisa estar vazio para aprovar.
- O `/plan` e o `/tdd` consomem a spec aprovada (AC-n → nome de teste).

Por isso, cada spec da seção 10 já vem organizada nessas 9 dimensões.

---

## 2. Visão do produto

### 2.1 Problema

Quero controlar meu computador Windows pelo celular, de casa ou fora dela: desligar/reiniciar, ver e controlar a tela, gerenciar arquivos, processos e apps, receber alertas e, principalmente, **acompanhar e conduzir o Claude Code** que roda no PC (responder, aprovar permissões, revisar diffs, fazer commit) sem precisar estar na frente do computador.

### 2.2 Usuário

- Usuário único (eu), dono do PC, desenvolvedor.
- Até 3 celulares/tablets pareados (ex.: meu celular, meu tablet, um aparelho "somente leitura").
- Um PC por agente; o app suporta vários PCs.

### 2.3 Componentes

| Componente | Tecnologia | Onde roda |
|---|---|---|
| **MyRemoteControll Agent** | C# / .NET 10, ASP.NET Core, SignalR | PC Windows 10/11, sessão do usuário (bandeja) |
| **MyRemoteControll Sensors** | C# / .NET 10, Worker Service | PC Windows, serviço elevado (somente sensores de hardware) |
| **MyRemoteControll App** | Flutter / Dart | Android e iOS |

### 2.4 Funcionalidades (escopo total do produto)

| Área | Funcionalidades |
|---|---|
| **Painel** | **Painel bonito, tela inicial do app, mostrando tudo do PC em tempo real** (seção 4.3 e spec 028) |
| Energia | Desligar, reiniciar, suspender, hibernar, bloquear, cancelar (ligar o PC remotamente está fora do escopo) |
| Tela | **Espelhamento ao vivo** (multi-monitor) com controle de mouse/teclado; **screenshot sob demanda** |
| Automação | **Agendamento** por horário, recorrência ou evento |
| Sistema | **Gerenciador de processos**; **abrir apps e URLs** favoritos |
| Arquivos | Navegar, buscar, baixar, enviar, renomear, mover, copiar, excluir (Lixeira), pré-visualizar (imagem, PDF, código) |
| Claude Code | Sessões gerenciadas e externas, chat em streaming, **permissões remotas**, hooks, **Git**, **comandos rápidos**, **voz**, **imagens**, **monitor de build/testes**, **preview do localhost**, **custo/uso** |
| Monitoramento | Tempo real, temperaturas, **histórico**, **alertas**, **widget**, **atalhos de voz do sistema** |
| Segurança | Pareamento, TLS, papéis/permissões, biometria, **botão de pânico**, **avisos no PC**, **auditoria** |
| Notificações | Push (FCM ou ntfy) com ações rápidas |

### 2.5 Fora de escopo (não-objetivos)

- **Ligar o PC remotamente (Wake-on-LAN ou similares).** O roteador é da operadora e não permite isso; desligar/reiniciar atende ao caso de uso (ADR-0011).
- Suporte a macOS/Linux no agente (pode virar ADR futuro).
- Acesso multiusuário/multi-inquilino ou conta em nuvem própria.
- Transferência de áudio do PC.
- Substituir o recurso nativo **Remote Control** do Claude Code (ver ADR-0004): o MyRemoteControll convive com ele.
- Execução de comandos de shell arbitrários enviados pelo app.

### 2.6 Contexto importante: Remote Control nativo do Claude Code

O Claude Code já possui o recurso **Remote Control** (`claude remote-control`, `/remote-control` ou `/rc`), que permite continuar uma sessão local pelo app Claude ou claude.ai/code, e desde agosto/2026 também iniciar sessões pelo celular. Ele exige plano Pro/Max (Team/Enterprise com habilitação de admin), uma conexão remota por instância e o terminal aberto. O MyRemoteControll **não compete** com ele: integra o Claude Code ao restante do controle do PC (Git, builds, arquivos, tela, energia, agendamentos), funciona sem depender do app Claude e oferece um botão "Abrir no Remote Control" quando o recurso nativo estiver disponível. A decisão está no ADR-0004.

---

## 3. Stack e versões de referência

> Pesquisado em 18/10/2026. **Sempre** confirmar a última versão estável antes de adicionar cada dependência.

### 3.1 Agente (.NET)

| Item | Escolha | Observações |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# 14 | Suporte até nov/2028. O .NET 11 (STS) sai em 10/11/2026; não usar. .NET 8/9 perdem suporte em 10/11/2026. |
| Solução | `.slnx`, `Directory.Build.props`, Central Package Management | Gerado pelo `/dotnet-init` |
| Web | ASP.NET Core Minimal APIs + `IEndpointGroup` (padrão do kit) | |
| Tempo real | ASP.NET Core SignalR (JSON; MessagePack opcional) | |
| Banco local | **EF Core 10 + SQLite** | ADR-0007 (o padrão do kit é PostgreSQL; aqui SQLite por ser app local) |
| Erros | **Result pattern** + ProblemDetails | Padrão do kit (ADR-002 do kit) |
| Tempo | `TimeProvider` | Padrão do kit |
| HTTP | `IHttpClientFactory` | Padrão do kit |
| Mediator | **Nenhum** ou `Mediator` (MIT, source-generated) | MediatR 13+ tem licença comercial (alerta do kit) |
| Validação | FluentValidation via endpoint filter | Conferir licença da versão |
| Logs | Serilog (arquivo rotativo) + OpenTelemetry opcional | |
| Testes | xUnit v3, `WebApplicationFactory`, Verify | Testcontainers não é necessário (SQLite) |
| MCP | **ModelContextProtocol.AspNetCore 1.x** (SDK oficial C#) | Servidor MCP local para permissões do Claude |
| Proxy | YARP | Preview do localhost |
| Sensores | **LibreHardwareMonitorLib** (≥ 0.9.6) + driver **PawnIO** | Exige administrador; WinRing0 foi substituído por PawnIO |
| Captura de tela | Windows.Graphics.Capture e/ou DXGI Desktop Duplication (Vortice.Windows) | ADR-0012 |
| WebRTC | SIPSorcery (+ encoder H.264/VP8) | ADR-0012, fase 4 |
| Push | FirebaseAdmin (FCM HTTP v1) ou ntfy | ADR-0013 |
| Bandeja | WinForms `NotifyIcon` em `net10.0-windows` | ADR-0003 |
| Notificações no PC | Toast nativo do Windows (app não empacotado) | Verificar biblioteca atual |

### 3.2 App (Flutter)

| Item | Escolha | Observações |
|---|---|---|
| SDK | **Flutter 3.47.x / Dart 3.13** | 3.50 previsto para nov/2026; avaliar upgrade |
| Design | **`material_ui`** (pacote standalone) | Desde o 3.47 Material/Cupertino são pacotes no pub.dev; usar `dart fix --apply --code=migrate_design_widgets` |
| Arquitetura | Camadas UI / Lógica / Dados (skill oficial) + feature-first | |
| Estado | **Riverpod 3.x** (`flutter_riverpod`, `riverpod_generator`, `riverpod_lint`) | |
| Navegação | `go_router` | Skill `flutter-setup-declarative-routing` |
| SignalR | `signalr_netcore` (1.4.x) | Verificar suporte a HttpClient customizado (TLS) |
| HTTP | `dio` ou `http` | Skill `flutter-use-http-package` |
| Armazenamento seguro | `flutter_secure_storage` | |
| QR | `mobile_scanner` | |
| Biometria | `local_auth` | |
| WebRTC | `flutter_webrtc` | |
| Voz | `speech_to_text` (pt-BR) | |
| Imagens/arquivos | `image_picker`, `file_picker`, `receive_sharing_intent` | |
| WebView | `webview_flutter` | |
| Gráficos | `fl_chart` | Gauges, sparklines, áreas e barras do painel |
| Animações | Animações implícitas/explícitas do Flutter (avaliar `flutter_animate`) | Transições suaves dos valores ao vivo |
| Tela ligada | `wakelock_plus` (ou equivalente atual) | Modo quiosque do painel |
| Haptics | `HapticFeedback` do Flutter | Confirmações de ações |
| Markdown/código | `flutter_markdown` (ou sucessor) + destaque de sintaxe | |
| Push | `firebase_messaging` + `flutter_local_notifications` | |
| Widget/atalhos | `home_widget`, `quick_actions`, App Intents (iOS) / atalhos (Android) | |
| Localização | `flutter_localizations` + `intl`, pt-BR | Skill `flutter-setup-localization` |
| JSON | `json_serializable` ou `freezed` | Skill `flutter-implement-json-serialization` |

---

## 4. Arquitetura geral

```
┌────────────────────────── Celular ───────────────────────────┐
│ MyRemoteControll App (Flutter)                               │
│  UI (telas) → ViewModels/Providers (Riverpod) → Repositórios │
│  → Serviços: SignalR, REST (dio), WebRTC, Push               │
└───────────────┬──────────────────────────────────────────────┘
                │ HTTPS/WSS (LAN ou Tailscale)
                ▼
┌──────────────────────── PC Windows ──────────┐
│ MyRemoteControll Agent (sessão do usuário)   │
│  Listener público :47800 (LAN/Tailscale)     │
│   ├─ REST /api/v1/*  ├─ Hub /hubs/remote     │
│   └─ Proxy YARP /preview/*                   │
│  Listener loopback 127.0.0.1:47801           │
│   ├─ MCP /mcp (permissões do Claude)         │
│   └─ Hooks /hooks/claude/* (HTTP hooks)      │
│  Features (vertical slices):                 │
│   Pairing · Auth · Audit · Power · Status    │
│   Claude · Git · Builds · Preview · Screen   │
│   Files · Processes · Favorites · Scheduler  │
│   Monitoring · Alerts · Push · Panic         │
│  SQLite (EF Core) · Serilog · Ícone bandeja  │
│        │ named pipe (somente leitura)        │
│        ▼                                     │
│ MyRemoteControll Sensors (serviço elevado)   │
│   LibreHardwareMonitorLib + PawnIO           │
│        │                                     │
│ Processos `claude` (headless stream-json)    │
│ Sessões `claude` no terminal (via hooks)     │
└──────────────────────────────────────────────┘
        │ push (FCM/ntfy) — só avisos, sem dados sensíveis
        ▼
     Celular
```

### 4.1 Princípios

- **Menor privilégio:** o agente roda sem elevação. Apenas o serviço de sensores roda elevado, e só expõe leitura de sensores por named pipe com ACL restrita ao usuário.
- **Dois listeners:** público (LAN/Tailscale, autenticado, TLS) e loopback (MCP e hooks do Claude, com token por sessão). Nada do loopback é acessível pela rede.
- **Ações bem definidas:** o app nunca envia comandos de shell; só invoca operações do contrato.
- **Tudo auditado:** cada operação mutável gera um registro de auditoria.
- **Tempo real por eventos:** SignalR para estado/streams; REST para arquivos e operações pontuais.

### 4.2 Integração com o Claude Code (três modos)

| Modo | Como funciona | Controle pelo app |
|---|---|---|
| **A. Sessões gerenciadas** | O agente inicia `claude -p --input-format stream-json --output-format stream-json --verbose` no diretório do projeto, lê stdout em JSON Lines e escreve mensagens no stdin. Permissões via `--permission-prompt-tool` apontando para o servidor MCP local (`--mcp-config`). Retomada com `--resume <id>` / `--continue`. | Total: chat, streaming, permissões, interromper, encerrar |
| **B. Sessões externas (terminal)** | Hooks HTTP do Claude Code (`SessionStart`, `Notification`, `PermissionRequest`, `Stop`, `StopFailure`, `SessionEnd`) postam no listener loopback. O `PermissionRequest` pode aguardar a decisão do celular. Para responder, o app "assume" a sessão retomando-a no modo A com `--resume`. | Notificações, aprovar permissões, assumir sessão |
| **C. Remote Control nativo** | Botão para iniciar `claude remote-control` em um projeto e abrir o link/QR no app Claude. | Delegado ao app oficial |

Formato de entrada stream-json (confirmar na documentação): cada linha é um objeto como
`{"type":"user","message":{"role":"user","content":[{"type":"text","text":"..."}]}}`.

Resposta da ferramenta de permissão (confirmar na documentação): string JSON com
`{"behavior":"allow","updatedInput":{...}}` ou `{"behavior":"deny","message":"..."}`. A ferramenta só é chamada quando nenhuma regra estática de allow/deny se aplica, e bloqueia a execução do Claude até responder.

### 4.3 Painel em tempo real e identidade visual

O **painel é a tela inicial do app** e o coração da experiência: precisa ser **bonito** e mostrar **tudo o que acontece no PC em tempo real**, num só lugar.

**Arquitetura do fluxo de dados**

```
Features do agente ──eventos──► DashboardAggregator ──► Hub: DashboardSnapshot (ao assinar)
 (status, métricas, Claude,        (estado consolidado,      DashboardDelta (só o que mudou,
  builds, git, agenda, alertas,     throttle por card,        com throttle por card)
  tela, arquivos, aparelhos)        filtra por permissão)
                                                   │
App: DashboardRepository ─► providers por card (Riverpod, seleção fina) ─► cards
```

- O agente mantém um **estado consolidado** (`DashboardState`) alimentado pelos eventos internos de cada feature.
- Ao abrir o painel, o app chama `SubscribeDashboard(options)` e recebe um **`DashboardSnapshot`** completo; depois recebe apenas **`DashboardDelta`** (patches por card, com número de versão para detectar perda e pedir novo snapshot).
- Frequência: métricas de hardware a cada 1 s (configurável 0,5–10 s); demais cards orientados a evento; throttle por card para não saturar a rede.
- O agregador **filtra pelo papel/permissões** do aparelho: cards sem permissão nem são enviados.
- No app, **cada card tem seu próprio provider** e só reconstrói quando o seu pedaço muda (nada de rebuild da tela inteira).
- App em segundo plano: cancela a assinatura; ao voltar, pede novo snapshot.
- Em dados móveis ou economia de bateria: reduz a frequência automaticamente (configurável).

**Diretrizes visuais (design system do app)**

- Material 3 via `material_ui`, com **tema próprio** em tokens (cores, tipografia, espaçamentos, raios, elevação, durações de animação) definidos uma única vez em `app/lib/app/theme/`.
- **Modo escuro como padrão** (confirmar no `/spec`), modo claro completo, opção de cor dinâmica (Material You) no Android.
- Cards com cantos arredondados, hierarquia clara (título, valor principal grande, detalhe secundário), ícones consistentes.
- **Visualizações vivas:** gauges circulares animados (CPU, RAM, GPU, disco, temperatura), sparklines dos últimos 60 s, gráficos de área para rede, barras por núcleo.
- **Cores semânticas por limiar** (normal / atenção / crítico) sempre acompanhadas de ícone ou texto (não depender só da cor).
- **Movimento:** transições suaves de valores (200–300 ms), indicador pulsante de "ao vivo", skeleton enquanto carrega, animações respeitando "reduzir movimento" do sistema.
- **Estados claros:** online, reconectando, desatualizado ("há 12 s"), offline (mostrando há quanto tempo e o último estado conhecido).
- **Responsivo:** 1–2 colunas no celular retrato, 3–4 colunas em tablet/paisagem (skill `flutter-build-responsive-layout`).
- **Haptics** em confirmações e ações importantes.
- Previews de widget (`flutter-add-widget-preview`) para todos os cards, nos estados normal, atenção, crítico, carregando, sem permissão e offline.

---

## 5. Estrutura do repositório

```
MyRemoteControll/
├── CLAUDE.md                         # raiz: regras gerais (seção 14)
├── .claude/
│   ├── rules/                        # 10 regras do dotnet-claude-kit + regras do projeto
│   └── settings.json                 # permissões (dotnet build/test/format, flutter ...)
├── docs/
│   ├── adr/                          # 0001-...md
│   ├── specs/                        # 001-...md (gerados pelo /spec)
│   ├── architecture/                 # contrato do Hub, modelo de dados, diagramas
│   └── base/MyRemoteControll-base-specs.md   # ESTE documento
├── agent/
│   ├── CLAUDE.md                     # gerado pelo /dotnet-init
│   ├── MyRemoteControll.slnx
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   ├── src/
│   │   ├── MyRemoteControll.Agent/        # host + features (vertical slices)
│   │   │   └── Features/<Feature>/...     # endpoints, handlers, hub methods, validação
│   │   ├── MyRemoteControll.Windows/      # interop Win32/WinRT (energia, input, captura, toasts)
│   │   ├── MyRemoteControll.Claude/       # ClaudeProcess, StreamJsonParser, MCP de permissões, hooks
│   │   ├── MyRemoteControll.Sensors/      # serviço elevado (LHM + named pipe)
│   │   └── MyRemoteControll.Contracts/    # DTOs e contratos do Hub compartilhados
│   ├── tests/
│   │   ├── MyRemoteControll.Agent.Tests/  # unidade
│   │   └── MyRemoteControll.Agent.IntegrationTests/  # WebApplicationFactory + SQLite
│   └── installer/                    # script de instalação/publicação
├── app/
│   ├── CLAUDE.md                     # regras do app (skills Flutter)
│   ├── pubspec.yaml                  # name: my_remote_controll
│   ├── lib/
│   │   ├── main.dart
│   │   ├── app/                      # bootstrap, tema, roteador, l10n
│   │   ├── core/                     # network, storage, security, logging, errors
│   │   └── features/<feature>/       # ui/, logic/ (viewmodels/providers), data/ (repos, services, models)
│   ├── test/ · integration_test/
│   └── l10n/                         # pt-BR (.arb)
└── tools/
    └── claude-hooks/                 # exemplos de settings.json de hooks para sessões externas
```

---

## 6. ADRs propostos

### 6.1 Formato de cada ADR (`docs/adr/NNNN-titulo.md`)

```markdown
# ADR-NNNN: <Título>
**Status:** Proposto | Aceito | Substituído por ADR-XXXX
**Data:** AAAA-MM-DD
## Contexto
## Decisão
## Alternativas consideradas
## Consequências (positivas / negativas / riscos)
## Specs afetadas
```

### 6.2 Lista de ADRs (decisão recomendada; validar comigo antes de aceitar)

| ADR | Título | Decisão recomendada | Alternativas | Specs |
|---|---|---|---|---|
| 0001 | Registro de decisões | Usar ADRs em `docs/adr/` no formato acima; specs referenciam ADRs | Wiki, sem registro | todas |
| 0002 | Arquitetura do agente | **Vertical Slice** (feature folders) em poucos projetos (Agent, Windows, Claude, Sensors, Contracts). Respostas ao `architecture-advisor`: domínio de complexidade baixa/média, 1 desenvolvedor, projeto de longa duração, muita integração com SO | Clean Architecture, Modular Monolith | todas |
| 0003 | Hospedagem do agente | App de **bandeja na sessão do usuário** (WinForms `NotifyIcon` + host genérico com Kestrel), iniciado por tarefa agendada "ao fazer logon", sem elevação | Windows Service puro (sessão 0 não captura tela nem interage com o terminal do usuário) | 001 |
| 0004 | Integração com o Claude Code | **Wrapper próprio** `ClaudeProcess` sobre o CLI (`-p` + stream-json bidirecional), permissões via **servidor MCP local** (`--permission-prompt-tool`) e hooks HTTP para sessões externas. Convive com o Remote Control nativo (botão de atalho) | SDKs .NET comunitários (não oficiais); usar apenas o Remote Control nativo (depende do app Claude, sem integração com Git/arquivos/tela); API direta da Anthropic (cobra por token e não usa a sessão do Claude Code) | 008–016 |
| 0005 | Transporte de tempo real | **SignalR** (WebSocket) para estado/eventos; REST para arquivos e operações idempotentes; contratos versionados em `MyRemoteControll.Contracts` | gRPC (suporte Flutter mais trabalhoso), WebSocket cru | todas |
| 0006 | TLS e confiança | Opção A (padrão): **certificado autoassinado** gerado no 1º uso + **pinning do fingerprint SHA-256** recebido no QR. Opção B: **certificado do Tailscale** (`tailscale cert`, domínio `*.ts.net`) quando o PC estiver na tailnet | HTTP puro (proibido), CA própria instalada no celular | 002 |
| 0007 | Persistência | **SQLite + EF Core 10**, arquivo em `%LOCALAPPDATA%\MyRemoteControll\data.db`, migrations versionadas | PostgreSQL (padrão do kit; desnecessário para app local), LiteDB | 001+ |
| 0008 | Autenticação | Pareamento por QR (token de uso único, 5 min) → **token de dispositivo** opaco de alta entropia, guardado com hash (SHA-256 + pepper) no PC; rotação; revogação | JWT com refresh, mTLS | 002, 003 |
| 0009 | Autorização | **Papéis** (Leitura, Operador, Administrador) + **permissões granulares** por dispositivo; policies do ASP.NET Core aplicadas a endpoints e métodos do Hub | Só papéis | 003 |
| 0010 | Conectividade externa | **Tailscale** (recomendado) ou Cloudflare Tunnel; **nunca** redirecionamento de porta no roteador | Port forwarding, relay próprio | 002 |
| 0011 | Ligar o PC remotamente | **Fora do escopo.** O roteador é da operadora (sem SSH nem WoL) e desligar/reiniciar atende ao caso de uso. Não implementar Wake-on-LAN | Aparelho auxiliar com Tailscale (Raspberry Pi, Android antigo), ESP32, tomada inteligente + BIOS "ligar ao voltar energia", relé no botão de power — reavaliar se a necessidade surgir | — |
| 0012 | Captura e streaming de tela | Fase 1: **MJPEG sobre WebSocket** (envio só com mudança, qualidade/FPS adaptativos). Fase 2: **WebRTC** (SIPSorcery no agente + flutter_webrtc no app, sinalização pelo Hub). Captura via **Windows.Graphics.Capture** com fallback **DXGI Desktop Duplication** | VNC/RDP embutido, H.264 cru por WebSocket | 017–019 |
| 0013 | Notificações push | **FCM** com o projeto Firebase do próprio usuário (FirebaseAdmin no agente) ou **ntfy** (público/auto-hospedado); payload só com tipo + IDs | Sem push (perde avisos em segundo plano) | 011 |
| 0014 | Sensores de hardware | **Serviço elevado separado** (`MyRemoteControll.Sensors`) com LibreHardwareMonitorLib + PawnIO, expondo leitura por **named pipe** com ACL do usuário | Agente inteiro elevado (amplia a superfície de ataque) | 024 |
| 0015 | Execução de Git | Executável `git` via `ProcessStartInfo.ArgumentList` (sem shell) com parser de saída porcelain v2 | LibGit2Sharp (sem suporte completo a credenciais/hooks do usuário) | 012 |
| 0016 | Arquitetura do app | Camadas **UI → Lógica (ViewModels/Notifiers Riverpod) → Dados (Repositórios → Serviços)**, feature-first, `go_router`, `material_ui` | BLoC, GetX | 004+ |
| 0017 | Preview do localhost | **Proxy reverso YARP autenticado** no agente (`/preview/{porta}/...`), sem alterar o bind do servidor de dev | Expor a porta de dev na rede | 015 |
| 0018 | Agendador | `BackgroundService` próprio com fila persistida no SQLite e `TimeProvider` (testável) | Quartz.NET (avaliar se a complexidade crescer) | 023 |
| 0019 | Distribuição | `dotnet publish` self-contained single-file win-x64 + instalador (Inno Setup ou script PowerShell); app via APK/TestFlight | MSIX (complica o serviço elevado e o autostart) | 027 |
| 0021 | Painel em tempo real | **Agregador no agente** (`DashboardAggregator`) com `DashboardSnapshot` + `DashboardDelta` versionados, filtrados por permissão; no app, um provider por card | App assinar cada feature separadamente (muitas assinaturas, rebuilds e tráfego) | 007, 028 e todas que criam cards |
| 0020 | Observabilidade | Serilog estruturado + correlation ID por requisição/invocação do Hub; logs nunca contêm tokens nem conteúdo de arquivos | — | todas |

---

## 7. Modelo de domínio e dados

> Entidades persistidas no SQLite (EF Core). Datas em `DateTimeOffset` UTC via `TimeProvider`.

| Entidade | Campos principais | Ciclo de vida / regras |
|---|---|---|
| `Device` | Id, Name, Platform, Role, CreatedAt, LastSeenAt, TokenHash, TokenRotatedAt, RevokedAt, PushToken?, IsReadOnly | Criado no pareamento; revogação é lógica e imediata (desconecta do Hub) |
| `DevicePermission` | DeviceId, PermissionKey | Concede permissões além do papel |
| `PairingSession` | Id, OneTimeCodeHash, ExpiresAt, ConsumedAt, CertFingerprint | Uso único, expira em 5 min |
| `AuditEntry` | Id, Timestamp, DeviceId, Action, TargetJson, Result, ErrorCode?, CorrelationId | Somente inserção; retenção configurável (padrão 90 dias) |
| `AllowedRoot` | Id, Path, Label, Kind (Files/ClaudeProject), CanWrite | Cadastrado no PC (bandeja) |
| `Favorite` | Id, Name, ExecutablePath, Arguments, WorkingDir, IconPng, Kind (App/Url) | Cadastro no PC ou pelo app com confirmação no PC |
| `ClaudeProject` | Id, AllowedRootId, Name, DefaultModel?, DefaultPermissionMode, BuildProfilesJson | Projeto da whitelist |
| `ClaudeSession` | Id (session_id do Claude), ProjectId, Origin (Managed/External), State, Title, StartedAt, LastEventAt, CostUsd?, TokensIn/Out? | Estados: Starting, Working, WaitingInput, WaitingPermission, Idle, Completed, Failed, Terminated |
| `ClaudeEvent` | Id, SessionId, Seq, Type, PayloadJson, CreatedAt | Buffer para reconexão; retenção por sessão (ex.: últimos 2.000 eventos) |
| `PermissionRequest` | Id, SessionId, ToolName, InputJson, Reason?, CreatedAt, DecidedAt?, Decision?, DecidedByDeviceId?, ExpiresAt | Pendente → Allowed/Denied/Expired |
| `QuickCommand` | Id, Title, Template, Scope (Global/Project), Order | Variáveis `{projeto}`, `{arquivo}`, `{branch}` |
| `BuildProfile` | Id, ProjectId, Name, Command, Args, Kind (Build/Test/Custom) | Executável + argumentos, nunca shell livre |
| `BuildRun` | Id, ProfileId, StartedAt, FinishedAt?, ExitCode?, Summary?, LogPath | Saída gravada em arquivo com rotação |
| `Schedule` | Id, Name, TriggerType, TriggerJson, ActionType, ActionJson, Enabled, NextRunAt?, CreatedByDeviceId | Persistido; reavaliado ao iniciar |
| `ScheduleRun` | Id, ScheduleId, FiredAt, Result, Cancelled | |
| `MetricSample` | Timestamp, Kind, Key, Value | Amostragem configurável; retenção padrão 7 dias; agregação por minuto/hora |
| `AlertRule` | Id, Metric, Operator, Threshold, DurationSeconds, Cooldown, Enabled, Channels | |
| `AlertEvent` | Id, RuleId, RaisedAt, ResolvedAt?, Value | |
| `AgentSettings` | Chave/valor tipados (Options pattern) | |

---

## 8. Contratos de API

### 8.1 Convenções

- Base REST: `https://<host>:47800/api/v1/...`; erros em **ProblemDetails** com `code` estável (ex.: `power.alreadyScheduled`).
- Autenticação: header `Authorization: Bearer <device-token>` (REST e negociação do Hub).
- Hub: `/hubs/remote`; métodos agrupados por prefixo; eventos do servidor em PascalCase.
- Paginação: `?cursor=&limit=` (limite máximo 200).
- Idempotência: operações mutáveis aceitam `Idempotency-Key`.
- Versão do contrato exposta em `GET /api/v1/info` (`apiVersion`, `agentVersion`, `capabilities[]`).

### 8.2 REST (principais)

| Verbo | Rota | Descrição | Permissão |
|---|---|---|---|
| POST | `/api/v1/pairing/complete` | Troca código do QR por token de dispositivo | (código de pareamento) |
| GET | `/api/v1/info` | Versão, capacidades, nome do PC | autenticado |
| GET | `/api/v1/devices` · DELETE `/api/v1/devices/{id}` | Listar/revogar dispositivos | `security.manageDevices` |
| GET | `/api/v1/audit` | Consultar auditoria | `security.viewAudit` |
| GET | `/api/v1/screen/screenshot?monitor=` | PNG | `screen.view` |
| GET | `/api/v1/files/list?root=&path=` | Listar diretório | `files.read` |
| GET | `/api/v1/files/download?root=&path=` | Download com Range | `files.read` |
| POST | `/api/v1/files/upload?root=&path=` | Upload multipart/streaming | `files.write` |
| POST | `/api/v1/files/ops` | Renomear/mover/copiar/criar pasta/excluir (Lixeira) | `files.write` / `files.delete` |
| GET | `/api/v1/files/preview?root=&path=` | Texto/código (limite de tamanho) | `files.read` |
| GET | `/api/v1/builds/{runId}/log` | Log completo | `dev.builds` |
| ANY | `/preview/{port}/{**path}` | Proxy YARP do localhost | `dev.preview` |
| GET | `/api/v1/metrics/history?kind=&from=&to=&resolution=` | Histórico | `monitor.view` |

### 8.3 Hub SignalR `/hubs/remote`

**Cliente → servidor (métodos):**

| Grupo | Métodos |
|---|---|
| Painel | `SubscribeDashboard(options)` → `DashboardSnapshot`, `UnsubscribeDashboard()`, `RequestDashboardSnapshot()`, `SetDashboardRate(intervalMs)` |
| Status | `GetStatus()`, `SubscribeMetrics(intervalMs)`, `UnsubscribeMetrics()` |
| Energia | `PowerAction(action, delaySeconds, force)`, `CancelPowerAction()` |
| Tela | `ListMonitors()`, `StartScreenStream(options)`, `StopScreenStream()`, `SendInput(batch)`, `WebRtcSignal(payload)` |
| Processos | `ListProcesses(query)`, `KillProcess(pid, tree)`, `ListWindows()`, `FocusWindow(hwnd)` |
| Favoritos | `ListFavorites()`, `OpenFavorite(id)`, `OpenUrl(url)`, `ProposeFavorite(dto)` |
| Agendador | `ListSchedules()`, `UpsertSchedule(dto)`, `DeleteSchedule(id)`, `RunScheduleNow(id)` |
| Claude | `ListProjects()`, `ListSessions(filter)`, `StartSession(projectId, options)`, `ResumeSession(sessionId)`, `SendMessage(sessionId, content[])`, `Interrupt(sessionId)`, `EndSession(sessionId)`, `RespondPermission(requestId, decision, scope)`, `GetSessionEvents(sessionId, afterSeq)`, `StartNativeRemoteControl(projectId)` |
| Git | `GitStatus(projectId)`, `GitDiff(projectId, path?, staged)`, `GitLog(projectId, limit)`, `GitCommit(projectId, message, paths?)`, `GitGenerateCommitMessage(projectId)`, `GitPush(projectId)`, `GitPull(projectId)`, `GitCreateBranch(projectId, name)`, `GitDiscard(projectId, paths?)` |
| Builds | `ListBuildProfiles(projectId)`, `RunBuild(profileId)`, `CancelBuild(runId)`, `AskClaudeToFix(runId, sessionId?)` |
| Preview | `ListListeningPorts()` |
| Comandos rápidos | `ListQuickCommands(projectId?)`, `UpsertQuickCommand(dto)` |
| Monitoramento | `ListAlertRules()`, `UpsertAlertRule(dto)` |
| Segurança | `Panic()`, `ListDevices()`, `UpdateDevicePermissions(id, dto)` |
| Push | `RegisterPushToken(provider, token)` |

**Servidor → cliente (eventos):**
`DashboardDelta(version, patches[])`, `StatusUpdated`, `MetricsTick`, `PowerCountdown`, `PowerCancelled`, `ScreenFrame` (MJPEG, binário), `WebRtcSignal`, `ClaudeEvent(sessionId, seq, event)`, `ClaudeSessionStateChanged`, `ClaudePermissionRequested`, `ClaudePermissionResolved`, `BuildOutput(runId, chunk)`, `BuildFinished`, `ScheduleFired`, `ScheduleCountdown`, `AlertRaised`, `AlertResolved`, `AuditAppended` (admins), `DeviceRevoked`, `PanicActivated`, `AgentShuttingDown`.

---

## 9. Autorização: papéis e permissões

| Permissão | Leitura | Operador | Administrador |
|---|:-:|:-:|:-:|
| `status.view`, `monitor.view` | ✅ | ✅ | ✅ |
| `screen.view` (espelhar/screenshot) | ✅ | ✅ | ✅ |
| `screen.control` (mouse/teclado) | | | ✅ |
| `power.basic` (bloquear, suspender) | | ✅ | ✅ |
| `power.shutdown` (desligar, reiniciar, hibernar) | | ✅ | ✅ |
| `processes.view` | ✅ | ✅ | ✅ |
| `processes.kill` | | ✅ | ✅ |
| `favorites.open` | | ✅ | ✅ |
| `favorites.manage` | | | ✅ |
| `files.read` | | ✅ | ✅ |
| `files.write` | | ✅ | ✅ |
| `files.delete` | | | ✅ |
| `claude.view` | ✅ | ✅ | ✅ |
| `claude.chat` | | ✅ | ✅ |
| `claude.approve` (permissões do Claude) | | | ✅ |
| `dev.git.read` | ✅ | ✅ | ✅ |
| `dev.git.write` (commit, push, pull, branch) | | ✅ | ✅ |
| `dev.git.discard` | | | ✅ |
| `dev.builds`, `dev.preview` | | ✅ | ✅ |
| `schedules.manage` | | ✅ | ✅ |
| `alerts.manage` | | | ✅ |
| `security.viewAudit`, `security.manageDevices` | | | ✅ |
| `security.panic` | ✅ | ✅ | ✅ |

Ações destrutivas exigem, no app, **biometria/PIN** antes do envio; o agente registra no audit se a confirmação local foi feita (`localConfirmation=true`).

---

## 10. Backlog de specs por fase

> Cada bloco abaixo é o **ponto de partida** do `/spec`. Formato: Problema · Escopo (In/Out) · Domínio · API · Autorização · Bordas e falhas · Não funcionais · Integrações · Critérios de aceite · Perguntas em aberto (para o `/spec` me fazer).

---

### FASE 0 — Fundação

#### Spec 001 — `agent-host`: host do agente, bandeja, configuração, logs e banco

- **Problema:** precisa existir um processo confiável no PC, iniciado com o Windows, que hospede a API e mostre seu estado.
- **Escopo In:** solução `.slnx` (via `/dotnet-init`); host genérico com Kestrel em dois listeners (público `0.0.0.0:47800`, loopback `127.0.0.1:47801`); ícone de bandeja (status, abrir logs, pausar acesso remoto, sair); instância única (mutex); autostart por tarefa agendada no logon; `appsettings.json` + Options pattern com validação; Serilog com arquivo rotativo em `%LOCALAPPDATA%\MyRemoteControll\logs`; SQLite + EF Core com migrations aplicadas na inicialização; health check `/health` (loopback); `GET /api/v1/info`.
- **Escopo Out:** autenticação (spec 002), qualquer feature de negócio.
- **Domínio:** `AgentSettings`.
- **API:** `GET /api/v1/info` (sem dados sensíveis antes de autenticar: apenas `apiVersion` e `requiresPairing`).
- **Autorização:** nenhuma ainda; tudo além de `/info` retorna 401.
- **Bordas:** porta ocupada (tentar porta alternativa configurável e avisar na bandeja); banco corrompido (backup automático e recriação com aviso); segunda instância (foca a existente e sai); encerramento gracioso (avisa clientes com `AgentShuttingDown`).
- **Não funcionais:** iniciar em < 3 s; consumo ocioso < 80 MB RAM e < 1% CPU.
- **Integrações:** Agendador de Tarefas do Windows (autostart).
- **Critérios de aceite:**
  - AC-1: Dado o agente instalado, quando faço logon no Windows, então o ícone aparece na bandeja em até 10 s.
  - AC-2: Dado o agente rodando, quando executo outra instância, então ela encerra e a existente continua.
  - AC-3: Dado o banco inexistente, quando o agente inicia, então as migrations criam o banco sem intervenção.
  - AC-4: Dado o agente rodando, quando chamo `/health` pelo loopback, então recebo 200; pela rede, recebo 404.
  - AC-5: Dado "Pausar acesso remoto" na bandeja, quando um cliente tenta conectar, então recebe 503 até eu retomar.
- **Perguntas em aberto:** porta padrão (47800 está ok?); autostart via tarefa agendada ou chave Run do registro?

#### Spec 002 — `pairing-auth`: pareamento, TLS e autenticação de dispositivos

- **Problema:** só meus aparelhos podem controlar o PC; a conexão precisa ser cifrada e resistente a interceptação.
- **Escopo In:** geração de certificado autoassinado (RSA 3072 ou ECDSA P-256, validade 5 anos) no 1º uso; opção de usar certificado do Tailscale (ADR-0006); QR de pareamento na bandeja com `{host(s), porta, códigoÚnico, fingerprintSHA256, nomeDoPC}`; código de uso único (5 min); `POST /pairing/complete` → token de dispositivo (256 bits) retornado uma única vez; hash do token no banco; rotação de token a cada 30 dias (transparente); revogação; app: tela de pareamento (QR via `mobile_scanner` e manual), armazenamento em `flutter_secure_storage`, pinning do fingerprint em REST, WebSocket e SignalR.
- **Escopo Out:** papéis e permissões finas (spec 003).
- **Domínio:** `Device`, `PairingSession`.
- **API:** `POST /api/v1/pairing/complete {code, deviceName, platform}` → `{deviceId, token, role}`.
- **Autorização:** o primeiro dispositivo pareado vira Administrador; os demais recebem Leitura até eu mudar (confirmar).
- **Bordas:** QR expirado; código reutilizado; fingerprint diferente (app bloqueia e mostra alerta de possível ataque); 5 falhas de pareamento em 10 min bloqueiam o pareamento por 15 min; relógio do celular errado não deve afetar; IP do PC mudou (QR tem vários hosts; app tenta todos e o hostname do Tailscale).
- **Não funcionais:** comparação de hash em tempo constante; tokens nunca em log.
- **Integrações:** Tailscale (opcional).
- **Critérios de aceite:**
  - AC-1: Dado um QR válido, quando o app o escaneia em até 5 min, então o app recebe um token e passa a conectar com sucesso.
  - AC-2: Dado um código já usado, quando é reenviado, então o agente responde 410 e registra auditoria.
  - AC-3: Dado um certificado com fingerprint diferente do pareado, quando o app conecta, então a conexão é recusada e o app alerta.
  - AC-4: Dado um dispositivo revogado, quando ele tenta qualquer chamada, então recebe 401 e, se conectado ao Hub, é desconectado em até 2 s.
  - AC-5: Dado 5 tentativas inválidas em 10 min, quando ocorre a 6ª, então o pareamento fica bloqueado por 15 min.
- **Perguntas em aberto:** papel padrão para novos aparelhos; suportar pareamento sem câmera (código numérico de 8 dígitos)?

#### Spec 003 — `authorization-audit`: papéis, permissões e auditoria

- **Problema:** aparelhos diferentes devem ter poderes diferentes, e tudo precisa ficar registrado.
- **Escopo In:** papéis e permissões da seção 9; policies do ASP.NET Core aplicadas a endpoints e a cada método do Hub (filtro de Hub); `AuditEntry` para toda ação mutável e para negações de acesso; consulta de auditoria com filtros; exportação CSV; retenção configurável; tela na bandeja para gerenciar dispositivos; telas no app (Segurança → Dispositivos, Auditoria).
- **Escopo Out:** botão de pânico (spec 026).
- **Domínio:** `DevicePermission`, `AuditEntry`.
- **API:** `GET /audit`, `GET/DELETE /devices`, `UpdateDevicePermissions` no Hub.
- **Bordas:** remover o último administrador (proibido); mudança de permissão com o aparelho conectado (aplica na hora); auditoria com volume alto (escrita em lote assíncrona sem perder registros no desligamento).
- **Não funcionais:** gravação de auditoria não pode adicionar > 5 ms à operação.
- **Critérios de aceite:**
  - AC-1: Dado um aparelho com papel Leitura, quando chama `PowerAction`, então recebe erro `auth.forbidden` e a negação é auditada.
  - AC-2: Dado qualquer ação mutável bem-sucedida, então existe um `AuditEntry` com dispositivo, ação, alvo, resultado e correlation ID.
  - AC-3: Dado que tento rebaixar o único administrador, então a operação é recusada com `security.lastAdmin`.
  - AC-4: Dado uma alteração de permissões, quando o aparelho afetado está conectado, então a nova política vale na próxima chamada.

#### Spec 004 — `app-shell`: base do app Flutter

- **Problema:** o app precisa de uma base sólida: arquitetura, navegação, conexão, vários PCs, segurança local.
- **Escopo In:** projeto `my_remote_controll` com `material_ui`, Riverpod 3, `go_router`, l10n pt-BR, **design system com tokens (seção 4.3)**, tema escuro e claro (Material 3), componentes base do painel (card, gauge, sparkline, badge de estado, indicador "ao vivo"), arquitetura em camadas (ADR-0016); `ConnectionManager` com reconexão exponencial, estados (Conectando, Online, Offline, Não autorizado, Certificado inválido) e troca rápida entre PCs; cliente REST (dio) e SignalR com pinning; bloqueio por biometria/PIN ao abrir e após N minutos em segundo plano; Dashboard vazio com cards por área; Configurações; telemetria zero.
- **Escopo Out:** funcionalidades de cada área.
- **Bordas:** sem rede; troca Wi-Fi ↔ dados móveis; PC dormindo; vários PCs com o mesmo nome; app reinstalado (tokens perdidos → reparear).
- **Não funcionais:** abertura a frio < 2 s em aparelho médio; acessibilidade (tamanhos de fonte, leitores de tela, contraste).
- **Integrações:** skills Flutter (arquitetura, roteamento, localização, JSON, testes).
- **Critérios de aceite:**
  - AC-1: Dado 2 PCs pareados, quando troco de PC no seletor, então o app reconecta ao novo em até 3 s.
  - AC-2: Dado a conexão perdida, quando a rede volta, então o app reconecta sozinho e reassina os eventos.
  - AC-3: Dado biometria ativada, quando abro o app após 5 min em segundo plano, então ele pede biometria antes de mostrar dados.
  - AC-4: Dado o app em inglês no sistema, então os textos continuam em pt-BR (idioma fixo) — **confirmar no /spec**.

---

### FASE 1 — Energia e status

#### Spec 005 — `power-actions`: desligar, reiniciar, suspender, hibernar, bloquear

- **Escopo In:** ações `Shutdown`, `Restart`, `Sleep`, `Hibernate`, `Lock`, `SignOut` (confirmar), `Cancel`; atraso (0, 30 s, 1 min, 5 min, personalizado até 24 h); opção "forçar fechamento de apps"; contagem regressiva no app e notificação no PC; aviso extra quando houver sessões do Claude trabalhando ou builds em execução; app: botões grandes com deslizar para confirmar + biometria.
- **Implementação de referência:** `shutdown /s|/r /t N [/f]`, `shutdown /a`, `SetSuspendState`, `LockWorkStation`; isolado em `IPowerService` (Windows) com fake para testes.
- **Bordas:** desligar é irreversível pelo app (não há como ligar o PC remotamente): o app deixa isso explícito na confirmação; ação já agendada (substituir ou recusar?); Windows Update pendente no reinício; hibernação desabilitada no sistema (erro claro); cancelamento após o prazo; conexão cai durante a contagem.
- **Critérios de aceite:**
  - AC-1: Dado `PowerAction(Shutdown, 60)`, então todos os clientes recebem `PowerCountdown` e o PC desliga em 60 s.
  - AC-2: Dado uma contagem ativa, quando chamo `CancelPowerAction`, então o desligamento é abortado e todos recebem `PowerCancelled`.
  - AC-3: Dado sessões do Claude no estado Working, quando peço desligar, então o app mostra o aviso e exige confirmação extra.
  - AC-4: Dado hibernação desabilitada, quando peço hibernar, então recebo `power.hibernateUnavailable`.
  - AC-5: Dado que confirmo Desligar, então a tela de confirmação avisa que o PC só poderá ser ligado presencialmente.

#### Spec 006 — removida

> Wake-on-LAN foi retirado do escopo (ADR-0011). A numeração foi mantida para não alterar as demais specs; não há nada a especificar aqui.

#### Spec 007 — `system-status-dashboard-v1`: status em tempo real e primeira versão do painel

- **Escopo In:** `GetStatus` (hostname, SO, uptime, usuário logado, IPs, bateria, estado de energia) e métricas (CPU total/por núcleo, RAM, discos, rede up/down, GPU básica) com intervalo 0,5–10 s; **infraestrutura do painel** (ADR-0021): `DashboardAggregator`, `SubscribeDashboard`, `DashboardSnapshot`, `DashboardDelta` versionado; **painel v1 no app** como tela inicial, já com o visual final (seção 4.3): cards de conexão/PC, energia, CPU, RAM, discos e rede com gauges e sparklines animados; temperaturas ficam para a spec 024; os demais cards entram nas specs de cada área.
- **Bordas:** contadores de desempenho indisponíveis; muitos clientes inscritos (um único amostrador compartilhado); delta perdido (versão fora de ordem → novo snapshot).
- **Critérios de aceite:**
  - AC-1: Dado o painel aberto, então as métricas atualizam no intervalo escolhido com transição animada.
  - AC-2: Dado nenhum cliente inscrito, então o amostrador de alta frequência para (economia).
  - AC-3: Dado um delta com versão fora de sequência, então o app pede novo snapshot automaticamente.
  - AC-4: Dado o PC offline, então o painel mostra o estado offline, há quanto tempo e o último estado conhecido.

---

### FASE 2 — Claude Code

#### Spec 008 — `claude-sessions`: sessões gerenciadas e chat em streaming

- **Problema:** conduzir o Claude Code pelo celular como se estivesse no terminal.
- **Escopo In:** whitelist de projetos (`ClaudeProject` ligado a `AllowedRoot`); `ClaudeProcess` (início, stdin/stdout/stderr, encerramento, kill da árvore no fim); `StreamJsonParser` tolerante a tipos desconhecidos (guarda o JSON cru); estados da sessão; buffer `ClaudeEvent` com `seq` para reconexão sem perda (`GetSessionEvents(afterSeq)`); várias sessões simultâneas (limite configurável, padrão 3); retomar com `--resume`; escolher modelo e modo de permissão (`default`, `acceptEdits`, `plan`); interromper; app: lista de sessões e tela de chat com Markdown, blocos de código com copiar, cards de ferramentas expansíveis, indicador de "trabalhando", rolagem inteligente.
- **Escopo Out:** permissões remotas (009), sessões de terminal (010), voz/imagens (013), custo (016).
- **Domínio:** `ClaudeProject`, `ClaudeSession`, `ClaudeEvent`.
- **Bordas:** `claude` não instalado ou não autenticado (mensagem clara com instrução de `claude` → login); processo morre (estado Failed + stderr no app); saída com linha JSON inválida; mensagem enviada enquanto o Claude trabalha (fila); app desconectado durante streaming (reenvio a partir do `seq`); encerramento do agente (sessões encerradas de forma limpa e retomáveis depois).
- **Não funcionais:** latência evento → celular < 300 ms na LAN; nenhum bloqueio da thread do Hub pela leitura de stdout.
- **Integrações:** Claude Code CLI (documentação atual obrigatória).
- **Critérios de aceite:**
  - AC-1: Dado um projeto permitido, quando inicio uma sessão e envio "liste os arquivos", então vejo a resposta chegando em streaming.
  - AC-2: Dado o app desconectado no meio de uma resposta, quando reconecta, então recebe os eventos perdidos na ordem, sem duplicar.
  - AC-3: Dado um diretório fora da whitelist, quando tento iniciar sessão nele, então recebo `claude.projectNotAllowed`.
  - AC-4: Dado uma sessão trabalhando, quando toco em Interromper, então a resposta para e a sessão volta a aceitar mensagens.
  - AC-5: Dado o CLI ausente, quando inicio sessão, então recebo `claude.cliNotFound` com orientação.

#### Spec 009 — `claude-permissions`: aprovar ou negar ações do Claude pelo celular

- **Escopo In:** servidor MCP local (ModelContextProtocol.AspNetCore, Streamable HTTP, só loopback, `AllowedHosts` restrito, token por sessão no header) com a ferramenta `approve`; cada sessão gerenciada recebe `--mcp-config` temporário e `--permission-prompt-tool mcp__myremotecontroll__approve`; a chamada cria `PermissionRequest`, emite `ClaudePermissionRequested` + push, e aguarda decisão; resposta `allow` (com `updatedInput` igual ao original), `deny` (com mensagem) ou "permitir sempre nesta sessão" (cache por ferramenta+padrão); timeout configurável (padrão 10 min) → negar; app: card com ferramenta, comando/arquivo e diff quando for edição, botões Aprovar / Negar / Sempre, exige `claude.approve` + biometria opcional.
- **Bordas:** dois aparelhos respondem ao mesmo tempo (primeira decisão vence, demais recebem `ClaudePermissionResolved`); sessão encerrada com pedido pendente; agente reinicia com pedido pendente (pedido expira e o Claude recebe negação); regras estáticas do `settings.json` já resolvem o pedido (o MCP nem é chamado — documentar no app).
- **Critérios de aceite:**
  - AC-1: Dado um pedido para executar `dotnet test`, quando aprovo no celular, então o Claude executa e o card mostra "Aprovado por <aparelho>".
  - AC-2: Dado um pedido sem resposta por 10 min, então o Claude recebe negação com a mensagem "Tempo esgotado".
  - AC-3: Dado "Sempre nesta sessão" para `Edit`, quando o Claude pede outra edição, então ela é aprovada automaticamente e auditada.
  - AC-4: Dado o endpoint MCP, quando acessado de outra máquina da rede, então a conexão é recusada.

#### Spec 010 — `claude-external-sessions`: sessões do terminal via hooks

- **Escopo In:** instalador de hooks (opcional, com confirmação) que adiciona ao `~/.claude/settings.json` hooks **HTTP** apontando para `http://127.0.0.1:47801/hooks/claude/<evento>` com token: `SessionStart`, `Notification` (matchers `permission_prompt`, `idle_prompt`), `PermissionRequest`, `Stop`, `StopFailure`, `SessionEnd`; registro de sessões externas (`Origin=External`); `PermissionRequest` externo aguarda decisão do celular até o timeout do hook e devolve a decisão no formato documentado; botão "Assumir sessão" (retoma com `--resume` como sessão gerenciada, avisando que o terminal deve ser fechado); listagem de sessões recentes a partir do histórico local do Claude Code (confirmar local e formato); botão "Abrir no Remote Control nativo" (modo C).
- **Bordas:** settings.json com hooks existentes (mesclar, nunca sobrescrever; backup); agente desligado com hooks instalados (hooks falham rápido e sem travar o Claude); duas sessões no mesmo diretório.
- **Critérios de aceite:**
  - AC-1: Dado uma sessão no terminal que termina uma tarefa, então recebo notificação "Claude terminou em <projeto>".
  - AC-2: Dado um pedido de permissão no terminal, quando aprovo no celular dentro do prazo, então o terminal segue sem intervenção local.
  - AC-3: Dado o agente parado, quando o Claude dispara um hook, então o Claude não trava (timeout curto) e segue o fluxo normal.
  - AC-4: Dado hooks pré-existentes, quando instalo os do MyRemoteControll, então os antigos são preservados e existe backup.

#### Spec 011 — `push-notifications`: avisos com o app em segundo plano

- **Escopo In:** provedores FCM (FirebaseAdmin com service account do meu projeto Firebase; arquivo fora do repositório) e ntfy (URL + tópico secreto); registro do token do aparelho (`RegisterPushToken`); tipos: permissão do Claude, sessão concluída/falhou, build falhou/passou, alerta, agendamento prestes a executar, PC ficou online (agente iniciou), dispositivo novo pareado; payload só com tipo + IDs; ações rápidas na notificação para permissões (Aprovar/Negar) exigindo desbloqueio; preferências por tipo no app; deduplicação e limite de frequência.
- **Bordas:** token inválido (remover); sem internet no PC (fila curta e descarte); app em primeiro plano (não duplicar com notificação local).
- **Critérios de aceite:**
  - AC-1: Dado o app fechado, quando o Claude pede permissão, então recebo push em até 5 s e, ao tocar, abro o card do pedido.
  - AC-2: Dado o tipo "build" desativado nas preferências, então nenhum push de build é enviado.
  - AC-3: Dado qualquer push, então o payload não contém caminhos, código ou conteúdo de mensagens.

---

### FASE 3 — Ferramentas de desenvolvimento

#### Spec 012 — `git-integration`

- **Escopo In:** por projeto: branch, ahead/behind, `status` (porcelain v2), arquivos alterados, diff por arquivo e total (unified, com limite de tamanho), log recente; ações: commit (seleção de arquivos, mensagem digitada ou gerada pelo Claude via sessão efêmera), push, pull (ff-only por padrão), criar/trocar branch, descartar arquivo/tudo (confirmação forte + biometria); app: tela Git com visualizador de diff (lado a lado em tablet, unificado no celular).
- **Implementação:** `git` com `ArgumentList` (ADR-0015); credenciais do próprio usuário (Git Credential Manager).
- **Bordas:** repositório em merge/rebase; conflitos; push rejeitado; credencial pedindo interação (falhar com mensagem clara, nunca travar); arquivos binários e enormes no diff.
- **Critérios de aceite:**
  - AC-1: Dado alterações feitas pelo Claude, quando abro Git, então vejo os arquivos e o diff de cada um.
  - AC-2: Dado "gerar mensagem", então recebo uma sugestão no padrão Conventional Commits que posso editar antes de commitar.
  - AC-3: Dado um pull com conflito, então nada é alterado e recebo `git.conflict` com a lista de arquivos.

#### Spec 013 — `claude-quick-commands-voice-images`

- **Escopo In:** comandos rápidos globais e por projeto com variáveis; ordenação; sugestões padrão ("Rode os testes", "Corrija os erros do build", "Faça o commit com uma mensagem descritiva", "Explique o que você alterou", "Revise o diff atual"); entrada por voz (pt-BR) com revisão antes de enviar; anexar imagens (câmera/galeria/print) com redimensionamento (lado maior ≤ 1568 px) e envio como bloco de imagem no stream-json (confirmar formato); colar texto da área de transferência do celular.
- **Critérios de aceite:**
  - AC-1: Dado um comando rápido com `{projeto}`, quando o uso, então o texto enviado tem o nome do projeto substituído.
  - AC-2: Dado uma foto de 12 MP, quando anexo, então ela é reduzida antes do envio e o Claude a recebe.
  - AC-3: Dado o ditado por voz, então o texto aparece editável e só é enviado ao tocar em Enviar.

#### Spec 014 — `build-test-monitor`

- **Escopo In:** perfis de build/teste por projeto (detecção automática de `*.slnx/*.sln`, `pubspec.yaml`, `package.json`); execução sem shell; saída em streaming; resumo de erros (parsers para MSBuild, `dotnet test`, `flutter test`, genérico); cancelamento; histórico; notificação de fim; botão "Pedir para o Claude corrigir" (envia a saída resumida para uma sessão nova ou existente).
- **Critérios de aceite:**
  - AC-1: Dado um build com erro, então recebo push "Build falhou: N erros" e vejo o resumo com arquivo:linha.
  - AC-2: Dado "Pedir para o Claude corrigir", então uma mensagem com os erros é enviada para a sessão escolhida.
  - AC-3: Dado um build em execução, quando cancelo, então a árvore de processos é encerrada.

#### Spec 015 — `localhost-preview`

- **Escopo In:** listar portas TCP em escuta com processo dono; proxy YARP autenticado `/preview/{porta}/...` com reescrita de cabeçalhos e WebSocket (hot reload); só portas em `127.0.0.1`/`::1`/`0.0.0.0` do próprio PC; app: WebView com recarregar, voltar, alternar user-agent mobile/desktop.
- **Bordas:** apps com caminhos absolutos (`/assets`) — documentar limitação e oferecer modo "subdomínio por porta" se necessário; cookies; portas do próprio agente (bloquear).
- **Critérios de aceite:**
  - AC-1: Dado um `npm run dev` na porta 5173, quando escolho a porta, então a página abre no celular e o hot reload funciona.
  - AC-2: Dado um aparelho sem `dev.preview`, então o proxy responde 403.

#### Spec 016 — `claude-usage-cost`

- **Escopo In:** extrair custo, tokens e duração das mensagens de resultado do stream-json (quando disponíveis); totais por sessão, dia, projeto; gráfico simples; aviso de orçamento diário configurável (notificação, sem bloquear).
- **Critérios de aceite:**
  - AC-1: Dado uma sessão concluída, então vejo custo e tokens na lista de sessões.
  - AC-2: Dado o orçamento diário excedido, então recebo um aviso uma única vez no dia.

---

### FASE 4 — Tela

#### Spec 017 — `screenshot`

- **Escopo In:** `ListMonitors`; screenshot de um monitor ou de todos (PNG); visualização com zoom; salvar na galeria; compartilhar; DPI e escala corretos.
- **Bordas:** tela bloqueada/UAC (área de trabalho segura não capturável — retornar `screen.secureDesktop`); monitores com escalas diferentes.
- **Critérios de aceite:**
  - AC-1: Dado 2 monitores, quando peço screenshot do monitor 2, então recebo apenas esse monitor na resolução nativa.
  - AC-2: Dado a tela de bloqueio ativa, então recebo `screen.secureDesktop`.

#### Spec 018 — `screen-stream-mjpeg-input`: espelhamento MJPEG e controle remoto

- **Escopo In:** captura (WGC com fallback DXGI); codificação JPEG com qualidade e FPS adaptativos (alvo 15–30 FPS na LAN, 5–10 em 4G); envio só quando houver mudança; redução de resolução configurável; seleção de monitor; modo somente visualização e modo controle (permissão `screen.control`); entrada via `SendInput` (mover, clique esquerdo/direito/meio, duplo clique, arrastar, rolagem, texto Unicode, atalhos Ctrl/Alt/Shift/Win, teclas especiais); app: modo touchpad e modo toque direto, pinça para zoom, teclado virtual com barra de teclas especiais, indicador de FPS/latência; borda colorida na tela do PC enquanto houver espectador + ícone da bandeja alterado.
- **Bordas:** perda de pacotes (descartar quadros antigos, nunca acumular fila); app em segundo plano (pausar stream); mudança de resolução no PC; múltiplos espectadores (um controlador por vez).
- **Não funcionais:** latência ponta a ponta < 200 ms na LAN; CPU do agente < 15% em 1080p/15 FPS.
- **Critérios de aceite:**
  - AC-1: Dado o espelhamento ativo na LAN, então vejo a tela com no mínimo 15 FPS.
  - AC-2: Dado o modo controle, quando toco num ponto no modo toque direto, então o clique ocorre na coordenada correspondente considerando escala/DPI.
  - AC-3: Dado alguém assistindo, então a borda de aviso aparece no PC e some ao encerrar.
  - AC-4: Dado um aparelho com papel Leitura, quando envia `SendInput`, então recebe `auth.forbidden`.

#### Spec 019 — `screen-stream-webrtc`

- **Escopo In:** WebRTC com SIPSorcery (agente) e flutter_webrtc (app); sinalização pelo Hub; codificação H.264 por hardware quando disponível (ou VP8); canal de dados para entrada (substitui o Hub para input); STUN opcional; sem TURN externo por padrão (Tailscale já resolve conectividade); fallback automático para MJPEG.
- **Critérios de aceite:**
  - AC-1: Dado WebRTC disponível, então o app usa WebRTC por padrão e mostra o modo no indicador.
  - AC-2: Dado falha na negociação, então o app volta para MJPEG em até 5 s sem ação manual.

---

### FASE 5 — Sistema

#### Spec 020 — `file-manager`

- **Escopo In:** raízes permitidas (`AllowedRoot`); normalização de caminhos e bloqueio de path traversal, links simbólicos/junctions que saiam da raiz, ADS (`:stream`) e nomes reservados do Windows; listar, ordenar, buscar por nome (e por conteúdo em projetos, com limite); download com Range/retomada; upload com streaming e progresso (limite configurável, padrão 2 GB); pasta "Inbox" padrão; renomear, mover, copiar, criar pasta; excluir para a **Lixeira**; pré-visualização de imagem, PDF, texto/código com destaque; compartilhar do celular para o PC (`receive_sharing_intent`).
- **Bordas:** arquivo em uso; nomes com caracteres inválidos; conflito de nome no upload (renomear automático); caminhos > 260 caracteres; raiz removida.
- **Critérios de aceite:**
  - AC-1: Dado `path=..\..\Windows`, então recebo `files.outsideRoot` e a tentativa é auditada.
  - AC-2: Dado um download interrompido, quando retomo, então ele continua do byte em que parou.
  - AC-3: Dado excluir um arquivo, então ele vai para a Lixeira, não é apagado definitivamente.
  - AC-4: Dado compartilhar 3 fotos da galeria para o MyRemoteControll, então elas chegam na Inbox do PC.

#### Spec 021 — `process-manager`

- **Escopo In:** listar processos (nome, PID, CPU %, memória, usuário, caminho, título da janela); busca e ordenação; detalhes; encerrar e encerrar árvore com confirmação; lista protegida (`csrss`, `wininit`, `winlogon`, `lsass`, `services`, `smss`, `System`, o próprio agente e o serviço de sensores); janelas abertas e trazer para frente.
- **Critérios de aceite:**
  - AC-1: Dado o processo `lsass`, quando tento encerrar, então recebo `processes.protected`.
  - AC-2: Dado um processo travado, quando encerro a árvore, então ele e os filhos terminam e a ação é auditada.

#### Spec 022 — `favorites-apps-urls`

- **Escopo In:** favoritos com ícone extraído do executável; abrir com argumentos e diretório de trabalho; URLs só `http/https` no navegador padrão; proposta de favorito pelo app exige confirmação no PC (notificação/diálogo da bandeja); histórico de aberturas.
- **Critérios de aceite:**
  - AC-1: Dado uma URL `file://` ou `javascript:`, então recebo `favorites.invalidUrl`.
  - AC-2: Dado um favorito proposto pelo app, então ele só aparece após eu confirmar no PC.

#### Spec 023 — `scheduler`

- **Escopo In:** gatilhos: data/hora única, recorrente (dias da semana + horário + fuso), "quando o processo X terminar", "quando a sessão do Claude terminar", "quando o build/teste terminar", "após N min de ociosidade do usuário", "quando a bateria chegar a X%"; ações: energia, abrir favorito, enviar mensagem a uma sessão do Claude, rodar perfil de build, notificar; aviso antecipado (padrão 60 s) com cancelamento no app e no PC; execução perdida com o PC desligado (pular e registrar, ou executar ao ligar — configurável por agendamento).
- **Critérios de aceite:**
  - AC-1: Dado "desligar quando a sessão X terminar", quando a sessão conclui, então recebo aviso e o PC desliga após 60 s se eu não cancelar.
  - AC-2: Dado um agendamento recorrente, então `NextRunAt` é recalculado corretamente após cada execução (testado com `FakeTimeProvider`).
  - AC-3: Dado o agente reiniciado, então os agendamentos continuam válidos.

---

### FASE 6 — Monitoramento

#### Spec 024 — `monitoring-history-alerts`

- **Escopo In:** serviço `MyRemoteControll.Sensors` elevado (ADR-0014) com LibreHardwareMonitorLib + PawnIO (instalação do driver pelo instalador, com consentimento); named pipe somente leitura; temperaturas (CPU, GPU, discos), ventoinhas, consumo; histórico no SQLite com agregação (bruto 24 h, por minuto 7 dias, por hora 90 dias — confirmar); gráficos 1 h/24 h/7 dias; regras de alerta (temperatura, disco livre, CPU sustentada, bateria, sessão do Claude aguardando há N min, agente offline — este detectado pelo app/push); cooldown e resolução de alertas.
- **Bordas:** PawnIO ausente (sensores mostram "indisponível" com instrução); serviço de sensores parado (agente segue sem temperaturas).
- **Critérios de aceite:**
  - AC-1: Dado CPU acima de 90 °C por 60 s, então recebo alerta uma vez e "resolvido" quando cair.
  - AC-2: Dado o serviço de sensores parado, então o restante do monitoramento continua funcionando.
  - AC-3: Dado o histórico de 7 dias, então o gráfico carrega em < 1 s.

#### Spec 028 — `realtime-dashboard`: painel completo em tempo real

> Numerada 028 para não renumerar o backlog, mas executada **nesta fase (6)**, depois que todas as áreas existem. A infraestrutura e o painel v1 vêm da spec 007; cada spec intermediária já adiciona o card da sua área (ver Definição de pronto, seção 13).

- **Problema:** quero abrir o app e ver **tudo** o que está acontecendo no PC, ao vivo, num painel bonito, sem navegar por várias telas.
- **Escopo In — cards do painel:**

  | Card | Conteúdo ao vivo | Ação rápida |
  |---|---|---|
  | PC e conexão | Online/offline, nome, uptime, usuário, latência, via LAN ou Tailscale | Trocar de PC |
  | Energia | Contagem regressiva ativa, próximo agendamento de energia | Cancelar, bloquear, desligar |
  | CPU | Gauge total, barras por núcleo, sparkline 60 s, clock | Abrir processos |
  | Memória | Gauge RAM, uso/total, sparkline | — |
  | GPU | Uso, VRAM, temperatura | — |
  | Discos | Uso por unidade, leitura/escrita | Abrir arquivos |
  | Rede | Gráfico de área up/down, totais | — |
  | Temperaturas e ventoinhas | CPU, GPU, discos, RPM, cores por limiar | Abrir monitoramento |
  | Bateria | Nível, carregando, tempo restante | — |
  | Claude Code | Sessões por estado (trabalhando, aguardando você, concluída), última atividade, custo do dia | Abrir sessão |
  | Permissões pendentes | Pedidos do Claude **no topo do painel** quando existirem | Aprovar / Negar |
  | Builds e testes | Execuções em andamento com última linha da saída, último resultado | Cancelar, pedir correção ao Claude |
  | Git | Por projeto: branch, arquivos alterados, ahead/behind | Abrir diff, commit |
  | Agendamentos | Próximos com contagem regressiva | Cancelar, executar agora |
  | Alertas | Alertas ativos com severidade | Ver detalhe, silenciar |
  | Top processos | Top 5 por CPU e por memória | Encerrar |
  | Tela | Miniatura ao vivo (≈1 quadro/s, opcional) e quem está assistindo | Abrir espelhamento, screenshot |
  | Transferências | Uploads/downloads em andamento com progresso | Cancelar |
  | Aparelhos | Aparelhos conectados agora | Revogar (admin) |
  | Atividade recente | Feed das últimas ações da auditoria | Abrir auditoria |

- **Escopo In — experiência:** grade responsiva (1–2 colunas no celular, 3–4 no tablet/paisagem); cards **reordenáveis, ocultáveis e com tamanhos** (pequeno, médio, grande), layout salvo por PC; toque no card abre a tela detalhada; **modo quiosque** para usar um tablet como monitor do PC (tela sempre ligada, tela cheia, sem ações destrutivas); seletor de frequência de atualização; indicador de "ao vivo" e de "desatualizado há X s".
- **Escopo Out:** painel web no PC (pode virar spec futura); painéis personalizados com fórmulas.
- **Domínio:** `DashboardState` (agente, em memória), `DashboardLayout` (app, por PC: ordem, visibilidade, tamanho de cada card).
- **API:** `SubscribeDashboard`, `DashboardSnapshot`, `DashboardDelta(version, patches[])`, `RequestDashboardSnapshot`, `SetDashboardRate` (seção 8.3).
- **Autorização:** cada card exige a permissão de leitura da sua área; ações rápidas exigem a permissão da ação; aparelhos com papel Leitura veem só cards permitidos e sem ações.
- **Bordas:** conexão instável (manter último valor com selo "desatualizado"); muitos projetos/sessões (card agrupa e mostra "+N"); valores ausentes (sensor indisponível → "—" com dica); PC offline (painel reduzido com último estado conhecido); vários aparelhos assistindo (um único agregador).
- **Não funcionais:** **60 fps** na rolagem e nas animações em aparelho intermediário; atualização visível < 1 s após a mudança no PC; snapshot inicial < 1,5 s na LAN; tráfego em repouso baixo (meta a definir no `/spec`, ex.: ≤ 5 KB/s sem miniatura de tela); respeitar "reduzir movimento" e fontes grandes.
- **Integrações:** todas as features do agente publicam eventos para o agregador.
- **Critérios de aceite:**
  - AC-1: Dado o app aberto, quando o painel carrega, então todos os cards permitidos aparecem preenchidos em até 1,5 s na LAN.
  - AC-2: Dado uma mudança de CPU, então apenas o card de CPU é reconstruído (teste de widget contando rebuilds) e o valor anima até o novo número.
  - AC-3: Dado um pedido de permissão do Claude, então o card de permissões aparece no topo com Aprovar/Negar e some quando resolvido em qualquer aparelho.
  - AC-4: Dado um tablet em paisagem, então o painel mostra pelo menos 3 colunas.
  - AC-5: Dado a conexão perdida, então os cards mostram "desatualizado há X s" e voltam ao vivo sozinhos quando a conexão retorna.
  - AC-6: Dado um aparelho com papel Leitura, então ele não recebe dados de cards de arquivos nem botões de ações destrutivas.
  - AC-7: Dado que reordeno e oculto cards, então o layout persiste após fechar o app e é independente por PC.
  - AC-8: Dado o modo quiosque, então a tela não apaga e ações destrutivas ficam indisponíveis.
  - AC-9: Dado "reduzir movimento" ativo no sistema, então as animações são substituídas por mudanças instantâneas.
- **Perguntas em aberto:** tema escuro como padrão? Cor de destaque da marca? Miniatura da tela ligada por padrão (consome dados)? Ordem inicial dos cards? Meta de consumo de dados em 4G?

#### Spec 025 — `widgets-shortcuts-voice-assistants`

- **Escopo In:** widget de tela inicial (Android/iOS) com status do PC e botões (bloquear, reiniciar, desligar); atalhos do ícone do app; Siri (App Intents) e Google Assistente (atalhos/App Actions): "Desligar meu computador", "Reiniciar meu computador", "Bloquear meu computador"; ações destrutivas a partir de widget/voz exigem desbloqueio do app.
- **Critérios de aceite:**
  - AC-1: Dado o widget, quando toco em Bloquear, então o PC bloqueia sem abrir o app (se a política permitir sem biometria) — **confirmar política no /spec**.
  - AC-2: Dado "Reiniciar meu computador" pela voz, então o app pede desbloqueio e confirmação antes de enviar a ação.

---

### FASE 7 — Segurança final e entrega

#### Spec 026 — `panic-pc-notifications-hardening`

- **Escopo In:** **botão de pânico** (app, widget e atalho global no PC): bloquear a tela, encerrar todas as sessões do Claude, cancelar builds, parar espelhamento, negar permissões pendentes e pausar o acesso remoto até liberação manual no PC; **toasts no PC** para ações remotas relevantes (configurável por tipo); timeout de sessão inativa no app; rate limiting por dispositivo; revisão final com `/security-scan` (OWASP, segredos, dependências vulneráveis) e checklist de endurecimento (cabeçalhos, Kestrel limits, tamanho máximo de mensagens do Hub, validação de entrada em todos os métodos).
- **Critérios de aceite:**
  - AC-1: Dado o pânico acionado, então em até 2 s o PC bloqueia, as sessões do Claude encerram e todos os aparelhos são desconectados.
  - AC-2: Dado o acesso pausado após pânico, então nenhuma chamada remota é aceita até eu liberar na bandeja.
  - AC-3: Dado um desligamento remoto, então aparece um toast no PC indicando o aparelho de origem.

#### Spec 027 — `packaging-release`

- **Escopo In:** `dotnet publish` self-contained single-file win-x64 (agente e serviço de sensores); instalador que: copia arquivos, cria tarefa de autostart, instala o serviço de sensores e o PawnIO (com consentimento), cria regra de firewall só para perfis Privado/Tailscale, gera o certificado; desinstalador limpo (remove hooks do Claude com backup); atualização (verificação de versão manual); app: build APK assinado e TestFlight; README completo (pré-requisitos, instalação, pareamento, Tailscale, hooks, Firebase/ntfy, solução de problemas).
- **Critérios de aceite:**
  - AC-1: Dado um Windows limpo, quando rodo o instalador, então em até 2 min consigo parear o app.
  - AC-2: Dado a desinstalação, então não restam tarefas, serviços, regras de firewall nem hooks do MyRemoteControll.

---

## 11. Requisitos não funcionais globais

| Categoria | Requisito |
|---|---|
| Segurança | TLS obrigatório; pinning; tokens com hash; menor privilégio; nenhum comando arbitrário; auditoria total; segredos fora do repositório (User Secrets/arquivo local protegido por DPAPI) |
| Privacidade | Nenhuma telemetria; push sem dados sensíveis; logs sem tokens/conteúdo |
| Desempenho | Ocioso: < 80 MB RAM, < 1% CPU; eventos na LAN < 300 ms; screenshot < 1 s |
| Painel e UX | Painel bonito e consistente com o design system (seção 4.3); 60 fps; atualização visível < 1 s; snapshot < 1,5 s; estados ao vivo/desatualizado/offline sempre visíveis |
| Confiabilidade | Reconexão automática; buffers com `seq`; encerramento gracioso; nenhum processo `claude` órfão |
| Compatibilidade | Windows 10 22H2+ e Windows 11 x64; Android 8+; iOS 16+ (confirmar mínimos com o Flutter 3.47) |
| Acessibilidade | Fontes dinâmicas, rótulos semânticos, contraste AA, alvos de toque ≥ 48 dp |
| Manutenibilidade | Vertical slices; contratos compartilhados; cobertura de testes nas partes críticas; `/arch-check` limpo |
| Testabilidade | Toda interação com o SO atrás de interfaces (`IPowerService`, `IScreenCapture`, `IInputInjector`, `IProcessRunner`, `IClaudeCli`) com fakes; `TimeProvider` em tudo que envolve tempo |

---

## 12. Riscos, limitações e pontos a verificar

| # | Risco / ponto | Mitigação |
|---|---|---|
| R1 | Flags e formatos do Claude Code mudam com frequência (`--input-format stream-json` já teve documentação incompleta) | Ler a documentação atual; isolar em `MyRemoteControll.Claude`; testes com gravações reais de stream-json; tolerar tipos desconhecidos |
| R2 | `--permission-prompt-tool` só é chamado quando nenhuma regra estática se aplica | Mostrar no app quais regras estáticas existem; documentar |
| R3 | Remote Control nativo pode cobrir parte do caso de uso | ADR-0004: convivência + atalho |
| R4 | `signalr_netcore` e pinning de certificado autoassinado | Verificar se aceita `HttpClient`/`WebSocket` customizado; alternativa: certificado do Tailscale (ADR-0006 B) |
| R5 | LibreHardwareMonitorLib exige administrador e driver PawnIO | Serviço elevado isolado (ADR-0014) |
| R6 | iOS: app em segundo plano não mantém WebSocket | Push para avisos |
| R13 | Sem Wake-on-LAN, um desligamento remoto por engano deixa o PC inacessível até alguém ligá-lo | Confirmação explícita, biometria, contagem regressiva com cancelamento; preferir Reiniciar/Bloquear quando possível |
| R7 | Captura de tela não alcança tela de bloqueio/UAC (área de trabalho segura) | Erro específico e documentação |
| R8 | WebRTC no .NET (SIPSorcery) e codificação H.264 por hardware podem ser trabalhosos | MJPEG primeiro (fase 4 começa por ele) |
| R9 | Licenças comerciais em pacotes populares (MediatR 13+, MassTransit 9+, FluentAssertions 8+, AutoMapper 15+) | `/outdated` e regra `packages` do kit |
| R10 | Material/Cupertino migrando para `material_ui`/`cupertino_ui`; dependências podem ainda usar o SDK | Começar com `material_ui`; checar compatibilidade dos pacotes |
| R11 | Flutter 3.50 e .NET 11 saem em nov/2026 | Manter Flutter estável atual e .NET 10 LTS; reavaliar após o lançamento |
| R12 | Local e formato do histórico de sessões do Claude Code (`~/.claude/...`) | Confirmar na documentação antes da spec 010 |

---

## 13. Convenções

- **Commits:** Conventional Commits em português, referenciando spec/ADR: `feat(claude): permissões remotas via MCP (spec 009, ADR-0004)`. Commits atômicos; nunca force-push na `main`.
- **Branches:** `spec/NNN-slug` por spec; PR com checklist dos ACs.
- **.NET:** regras do kit (C# 14, `sealed`, primary constructors, records, file-scoped namespaces, `CancellationToken` em tudo, `TimeProvider`, Result pattern, ProblemDetails, sem `async void`, sem `DateTime.Now`, sem `new HttpClient()`); hooks do kit para formatar após edição; `dotnet format --verify-no-changes` no pre-commit.
- **Flutter:** `flutter analyze` sem avisos; `riverpod_lint`; `dart format`; widgets pequenos e testáveis; previews de widget para componentes principais.
- **Testes:** AC-n → teste nomeado `AC{n}_<descrição>` (.NET) e `ac{n}_<descricao>` (Dart). Integração do agente com `WebApplicationFactory` + SQLite em arquivo temporário; widget tests e integration tests no app.
- **Definição de pronto por spec:** ACs passando, `/verify` verde, `flutter analyze/test` verdes, `/code-review` sem itens críticos, auditoria e permissões cobertas, documentação atualizada, e **a área da spec publica seus dados no `DashboardAggregator` e tem seu card no painel** (seguindo o design system, com preview de widget e atualização em tempo real).

---

## 14. Trecho sugerido para o CLAUDE.md

Colar na raiz (`CLAUDE.md`), além do conteúdo gerado pelo `/dotnet-init` em `agent/CLAUDE.md`:

```markdown
# MyRemoteControll

## Regras de ferramentas
- Para TODO código C#/.NET: use SEMPRE o plugin dotnet-claude-kit (skills, agentes, comandos e o MCP Roslyn).
  Fluxo: /spec → /plan → /tdd ou /scaffold → /verify → /code-review. Use /build-fix, /security-scan, /arch-check, /outdated, /checkpoint e /wrap-up.
- Para TODO código Flutter/Dart: use SEMPRE as skills de Flutter instaladas (plugin dart-flutter e dart-lang/skills) e o MCP do Dart/Flutter.
  Antes de cada tarefa no app, liste as skills disponíveis e carregue as que se aplicam.
- Nunca use versões de pacotes de memória; verifique a última versão estável e a licença.
- Para o Claude Code CLI (stream-json, hooks, MCP, permissões), consulte https://code.claude.com/docs antes de implementar.

## Documentos
- Base: docs/base/MyRemoteControll-base-specs.md
- ADRs: docs/adr/ (decisões aceitas são obrigatórias)
- Specs: docs/specs/ (só implemente specs com status Approved)

## Painel
- O painel em tempo real é a tela inicial e deve ser bonito (design system em docs/base, seção 4.3).
- Toda feature nova publica seus dados no DashboardAggregator e ganha um card no painel.

## Idioma
- Specs, ADRs, comentários e UI em pt-BR; identificadores de código em inglês.
```

---

## 15. Referências

- dotnet-claude-kit (repositório): https://github.com/codewithmukesh/dotnet-claude-kit
- Guia do dotnet-claude-kit: https://codewithmukesh.com/blog/dotnet-claude-kit-guide/
- Skill `/spec` do kit: https://github.com/codewithmukesh/dotnet-claude-kit/blob/main/skills/spec/SKILL.md
- Roteamento de agentes do kit: https://github.com/codewithmukesh/dotnet-claude-kit/blob/main/AGENTS.md
- Flutter agent plugins (skills oficiais): https://github.com/flutter/agent-plugins
- Agent skills para Flutter e Dart: https://docs.flutter.dev/ai/agent-skills
- Claude Code modo headless: https://code.claude.com/docs/en/headless
- Claude Code hooks: https://code.claude.com/docs/en/hooks
- Claude Code Remote Control: https://code.claude.com/docs/en/remote-control
- MCP C# SDK: https://csharp.sdk.modelcontextprotocol.io/
- Migração para material_ui/cupertino_ui: https://docs.flutter.dev/release/breaking-changes/material-ui-and-cupertino-ui
- Novidades do Flutter 3.47: https://flutter.dev/blog/whats-new-in-flutter-3-47
- signalr_netcore: https://pub.dev/packages/signalr_netcore
- Windows.Graphics.Capture: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture
- LibreHardwareMonitor: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
