# MyRemoteControll — Prompt de desenvolvimento

> Cole este documento inteiro na IA que vai desenvolver o projeto.

---

## 1. Papel

Você é um engenheiro de software sênior especialista em **C# / .NET 8, ASP.NET Core, SignalR, Win32/P-Invoke, WebRTC e Flutter/Dart**. Vai construir um sistema completo, funcional e pronto para uso, com código limpo, organizado, testado e comentado onde for necessário.

## 2. Objetivo

Construir o **MyRemoteControll**, um sistema para controlar meu computador Windows pelo celular, composto por:

1. **MyRemoteControll Agent** (C# / .NET 8): roda no computador, expõe uma API segura e executa as ações.
2. **MyRemoteControll App** (Flutter): Android e iOS, conecta ao agente e oferece a interface de controle.

Resumo das funcionalidades:

| Área | Funcionalidades |
|---|---|
| Energia | Desligar, reiniciar, suspender, bloquear (ligar o PC remotamente está fora do escopo) |
| Tela | **Espelhamento ao vivo** com controle remoto de mouse/teclado, **screenshot sob demanda** |
| Automação | **Agendamento** de ações por horário ou por evento |
| Sistema | **Gerenciador de processos**, **abrir apps e URLs** favoritos |
| Arquivos | Navegador de arquivos, download, upload, visualizador de código/imagens/PDF, busca |
| Claude Code | Sessões, chat em streaming, permissões remotas, Git, comandos rápidos, voz, fotos, monitor de build/testes, preview do localhost, custo/uso |
| Monitoramento | Status em tempo real, histórico, alertas, widget, atalhos de voz do sistema |
| Segurança | Pareamento seguro, permissões por dispositivo, botão de pânico, auditoria, avisos no PC |

---

## 3. Arquitetura geral

```
[Celular: MyRemoteControll App (Flutter)]
        │  HTTPS / WSS (SignalR) / WebRTC
        │  rede local ou Tailscale
        ▼
[PC: MyRemoteControll Agent (.NET 8, app de bandeja)]
        ├── Energia, processos, apps, arquivos, monitoramento
        ├── Captura de tela + entrada remota
        ├── Agendador
        └── Gerenciador do Claude Code (processos `claude` + servidor MCP local + hooks)
```

### Conectividade
- Rede local por padrão.
- Acesso externo via **Tailscale** (recomendado) ou Cloudflare Tunnel. **Nunca** expor a porta do agente diretamente na internet com redirecionamento de portas.

---

## 4. MyRemoteControll Agent (C# / .NET 8)

### 4.1 Estrutura da solução

```
MyRemoteControll.sln
├── src/
│   ├── MyRemoteControll.Agent/        # Host: ASP.NET Core (Kestrel) + SignalR + ícone de bandeja
│   ├── MyRemoteControll.Core/         # Domínio: energia, processos, arquivos, agendador, monitoramento
│   ├── MyRemoteControll.Claude/       # Integração com Claude Code (processos, stream-json, MCP, hooks)
│   ├── MyRemoteControll.Screen/       # Captura de tela, codificação, WebRTC, entrada remota
│   ├── MyRemoteControll.Infrastructure/ # EF Core + SQLite, Win32 interop, notificações
│   └── MyRemoteControll.Contracts/    # DTOs e contratos do Hub compartilhados
└── tests/
    └── MyRemoteControll.Tests/        # xUnit
```

### 4.2 Requisitos gerais
- Roda como **aplicativo de bandeja na sessão do usuário**, iniciado com o Windows (entrada no Agendador de Tarefas "ao fazer logon"). Não usar Windows Service puro: serviços rodam na sessão 0 e não conseguem capturar a tela nem interagir com o ambiente onde o Claude Code roda.
- Comunicação: **SignalR** (WebSocket) para tempo real + REST para pareamento, upload/download e status.
- Persistência: **SQLite com EF Core** (dispositivos, auditoria, agendamentos, histórico de métricas, favoritos).
- Logs com **Serilog** (arquivo rotativo). Configuração em `appsettings.json` + tela de configurações na bandeja.
- Menu da bandeja: status, QR code de pareamento, dispositivos pareados (permissões/revogar), pastas permitidas, apps favoritos, abrir logs, pausar acesso remoto, sair.
- Publicação: `dotnet publish` self-contained, single-file, win-x64, e um instalador simples (ex.: Inno Setup ou script PowerShell).

### 4.3 Energia
- Desligar: `shutdown /s /t {s}`; Reiniciar: `shutdown /r /t {s}`; Cancelar: `shutdown /a`.
- Suspender e bloquear via P/Invoke (`SetSuspendState`, `LockWorkStation`).
- Atraso configurável (agora, 30 s, 1 min, 5 min, personalizado) com cancelamento pelo app.
- Ao receber uma ação de energia com sessões do Claude ativas, avisar no app e pedir confirmação extra.

### 4.4 Espelhamento de tela ao vivo e screenshot
- Captura com **Desktop Duplication API (DXGI)** via `Vortice.Windows` (ou Windows.Graphics.Capture), com suporte a **múltiplos monitores** (escolher no app).
- Transporte em duas etapas:
  1. **MJPEG sobre WebSocket**: envia apenas quando há mudança na tela, qualidade e FPS adaptativos conforme a rede. Implementar primeiro.
  2. **WebRTC** com **SIPSorcery** no agente e **flutter_webrtc** no app, codificação H.264 (preferir encoder de hardware via Media Foundation), sinalização pelo Hub SignalR. Menor latência; vira o modo padrão quando estiver estável.
- **Controle remoto**: mouse (mover, clique esquerdo/direito, rolagem, arrastar) e teclado (texto e atalhos como Ctrl+C, Alt+Tab, Win) via `SendInput`. No app: modo touchpad e modo toque direto, zoom com pinça, teclado virtual com barra de teclas especiais.
- Modo **somente visualização** separado do modo controle (permissão diferente).
- **Screenshot sob demanda**: captura de um monitor ou de todos, PNG, com opção de salvar na galeria e compartilhar.
- Privacidade: enquanto alguém estiver vendo a tela, mostrar uma **borda colorida** na tela do PC e mudar o ícone da bandeja.
- Documentar limitação: a tela de bloqueio e prompts de UAC (área de trabalho segura) não podem ser capturados por um app na sessão do usuário.

### 4.5 Agendamento
- Serviço de agendamento persistido no SQLite (Quartz.NET ou `BackgroundService` próprio).
- **Gatilhos**: data/hora única, recorrente (dias da semana + horário), "quando o processo X terminar", "quando a sessão do Claude terminar", "quando o build/teste terminar", "após N minutos de ociosidade", "quando a bateria chegar a X%".
- **Ações**: desligar, reiniciar, suspender, bloquear, abrir app/URL favorito, enviar mensagem para uma sessão do Claude, notificar o celular.
- Contagem regressiva visível no app e notificação no PC antes de ações de energia, com opção de cancelar.

### 4.6 Gerenciador de processos
- Listar processos com nome, PID, CPU %, memória, usuário, título da janela; ordenar e buscar.
- Encerrar processo (com confirmação) e "encerrar árvore de processos".
- Lista de processos críticos protegidos que não podem ser encerrados (ex.: `csrss`, `wininit`, `lsass`, o próprio agente).
- Ver apps com janela aberta e trazer uma janela para frente.

### 4.7 Abrir apps e URLs
- **Favoritos** cadastrados no PC (bandeja) ou pelo app com confirmação no PC: nome, caminho do executável/atalho, argumentos, ícone extraído do executável.
- URLs: apenas `http`/`https`, abertas no navegador padrão.
- O agente **nunca** executa caminhos ou comandos arbitrários enviados pelo app; somente favoritos cadastrados.

### 4.8 Arquivos
- Acesso restrito a **pastas raiz permitidas** (whitelist), com proteção contra path traversal e links simbólicos que escapem da raiz.
- Navegar, ordenar, buscar por nome (e por conteúdo em pastas de código, com limite).
- **Download** com streaming, suporte a retomada (HTTP Range) e progresso.
- **Upload** do celular (fotos, arquivos, compartilhar a partir de outros apps) para uma pasta escolhida ou para a pasta "Inbox" padrão.
- Criar pasta, renomear, mover, copiar; **excluir enviando para a Lixeira**.
- Pré-visualização: imagens, PDF, texto e **código com destaque de sintaxe**.
- Limites de tamanho configuráveis.

### 4.9 Claude Code
O agente gerencia sessões do **Claude Code CLI** (`claude`) instalado no PC.

> IMPORTANTE: antes de implementar, consulte a documentação atual do Claude Code (https://docs.claude.com) para confirmar flags, o formato do stream-json, hooks, MCP e o mecanismo de permissões. Não invente flags; se algo mudou, adapte e explique.

**Sessões**
- Iniciar sessões em modo headless com streaming estruturado, por exemplo:
  `claude -p --input-format stream-json --output-format stream-json --verbose`
  no diretório do projeto (cwd), lendo stdout linha a linha (JSON Lines) e enviando mensagens por stdin.
- Retomar sessões existentes, inclusive as iniciadas manualmente no terminal (`--resume <session_id>` / `--continue`). Listar sessões recentes a partir do histórico local do Claude Code.
- Escolher modelo e modo de permissão (padrão, aceitar edições, plano) ao criar a sessão.
- Interromper resposta em andamento, encerrar sessão, várias sessões simultâneas, tratamento de processos órfãos e reconexão do app sem perder mensagens (buffer no agente).
- Só iniciar o Claude em **diretórios de projeto permitidos** (whitelist).
- Encapsular em `IClaudeSessionManager` / `ClaudeProcess` / `StreamJsonParser`.

**Permissões remotas**
- Encaminhar pedidos de permissão do Claude (editar arquivo, executar comando, acessar web etc.) para o celular, com Aprovar / Negar / "Aprovar sempre nesta sessão".
- Implementar com o mecanismo oficial (ex.: `--permission-prompt-tool` apontando para um **servidor MCP local** exposto pelo próprio agente) ou hooks, conforme a documentação.
- Timeout configurável: se ninguém responder, negar por padrão.

**Hooks**
- Instalar/configurar hooks do Claude Code (ex.: `Notification`, `Stop`) que avisam o agente quando uma sessão iniciada no terminal termina ou precisa de atenção, para gerar notificação no celular.

**Git**
- Por projeto: branch atual, `git status`, lista de arquivos alterados, **diff completo** (por arquivo e da sessão), log recente.
- Ações: commit (mensagem digitada ou gerada pelo Claude), push, pull, criar branch, descartar alterações de um arquivo ou de tudo (com confirmação forte).
- Usar o executável `git` com argumentos montados de forma segura (sem concatenar strings de shell) ou LibGit2Sharp.

**Comandos rápidos**
- Templates de prompt configuráveis, ex.: "Rode os testes", "Corrija os erros do build", "Faça o commit com uma mensagem descritiva", "Explique o que você alterou", com variáveis (`{projeto}`, `{arquivo}`).

**Voz e imagens**
- Entrada por voz no app (pacote `speech_to_text`, em pt-BR), revisável antes de enviar.
- Anexar foto/print/imagem ao prompt (câmera ou galeria), enviada ao Claude como bloco de imagem no stream-json (confirmar o formato na documentação).

**Monitor de build e testes**
- Comandos de build/teste configuráveis por projeto (ex.: `dotnet build`, `dotnet test`, `flutter test`, `npm test`), executados pelo agente com saída em streaming para o app.
- Detectar sucesso/falha pelo exit code, resumir erros, notificar no celular e oferecer o botão "Pedir para o Claude corrigir" com a saída do erro.

**Preview do localhost**
- Listar portas em escuta no PC (`IPGlobalProperties` / `GetExtendedTcpTable`) com o processo dono.
- Expor o servidor de desenvolvimento escolhido ao app por **proxy reverso autenticado no agente** (YARP), sem alterar o bind do servidor de dev. O app abre em `webview_flutter`, com recarregar e alternar entre visual mobile/desktop.

**Custo e uso**
- Exibir por sessão e por dia: tokens, custo e duração, a partir das mensagens de resultado do stream-json (quando disponíveis). Gráfico simples no app.

### 4.10 Monitoramento
- Tempo real: CPU (total e por núcleo), RAM, GPU, discos (uso e espaço livre), rede (up/down), **temperaturas** (via `LibreHardwareMonitorLib`; documentar que alguns sensores exigem execução como administrador), bateria, uptime, usuário logado.
- **Histórico** salvo no SQLite (amostras a cada X segundos, retenção configurável, padrão 7 dias) e gráficos no app (1 h, 24 h, 7 dias).
- **Alertas** configuráveis: temperatura acima de X, disco abaixo de X%, uso de CPU alto por mais de N minutos, bateria fraca, PC ficou offline (detectado pelo app/push), sessão do Claude aguardando há mais de N minutos.

### 4.11 Notificações push
- SignalR cai quando o app vai para segundo plano, então notificações importantes (permissão do Claude, tarefa concluída, alertas, agendamentos) usam **push**:
  - Opção principal: **Firebase Cloud Messaging** com o projeto Firebase do próprio usuário (`FirebaseAdmin` no agente).
  - Alternativa: **ntfy** (servidor público ou auto-hospedado).
- O conteúdo do push não deve conter dados sensíveis (código, caminhos completos); apenas o aviso e um ID para abrir a tela certa no app.
- Ação direta na notificação: "Aprovar" / "Negar" para permissões do Claude (exigindo biometria ao abrir, se configurado).

### 4.12 Segurança
- **HTTPS/WSS** com certificado autoassinado gerado na primeira execução; o app faz **certificate pinning** do fingerprint recebido no QR de pareamento.
- **Pareamento**: token de uso único com expiração de 5 minutos, exibido em QR na bandeja. Após parear, o agente emite um token de dispositivo de longa duração (armazenado com hash no PC e no `flutter_secure_storage` no celular). Rotação de tokens.
- **Permissões por dispositivo** (papéis + permissões granulares):
  - *Somente leitura*: status, monitoramento, ver tela, ver sessões.
  - *Operador*: + energia, processos, apps, arquivos (sem excluir), chat com o Claude.
  - *Administrador*: tudo, incluindo controle remoto da tela, excluir arquivos, Git destrutivo, aprovar permissões do Claude, gerenciar dispositivos.
- **Botão de pânico** (no app e por atalho no PC): bloquear a tela, encerrar todas as sessões do Claude, parar espelhamento e pausar o acesso remoto até liberação manual no PC.
- **Aviso no PC**: notificação nativa do Windows (toast) a cada comando remoto relevante, configurável por tipo.
- **Auditoria**: log de todas as ações (dispositivo, data/hora, ação, parâmetros, resultado), visível no app e na bandeja, com exportação.
- **Biometria/PIN** para abrir o app e para ações destrutivas (desligar, excluir, encerrar processo, controle remoto, descartar no Git).
- Rate limiting, bloqueio após tentativas falhas de pareamento, timeout de sessão inativa.
- O agente nunca executa comandos arbitrários vindos do app. Segredos fora do código-fonte.

### 4.13 Contrato do Hub SignalR
Antes de codificar, defina e apresente o contrato completo do Hub (`/hubs/remote`), agrupado por área, por exemplo:
- Servidor → `GetStatus`, `PowerAction`, `CancelPowerAction`, `ListProcesses`, `KillProcess`, `OpenFavorite`, `OpenUrl`, `ListSchedules`, `UpsertSchedule`, `DeleteSchedule`, `StartScreenStream`, `StopScreenStream`, `SendInput`, `TakeScreenshot`, `ListClaudeSessions`, `StartClaudeSession`, `SendClaudeMessage`, `InterruptClaude`, `RespondPermission`, `GitStatus`, `GitDiff`, `GitCommit`, `GitPush`, `RunBuild`, `ListPorts`, `Panic`…
- Cliente ← `StatusUpdated`, `MetricsTick`, `PowerCountdown`, `ClaudeEvent`, `ClaudePermissionRequested`, `ClaudeSessionStateChanged`, `BuildOutput`, `BuildFinished`, `ScheduleFired`, `AlertRaised`, `WebRtcSignal`…
Endpoints REST para pareamento, upload/download de arquivos e screenshot em PNG.

---

## 5. MyRemoteControll App (Flutter)

### 5.1 Requisitos gerais
- Flutter estável mais recente, Dart null-safety, **Material 3**, tema claro/escuro, textos em **português do Brasil**.
- Nome do app: **MyRemoteControll**; pacote Dart: `my_remote_controll`.
- Estado com **Riverpod**; navegação com **go_router**; estrutura **feature-first**:
  `features/pairing`, `features/dashboard`, `features/power`, `features/screen`, `features/schedules`, `features/processes`, `features/apps`, `features/files`, `features/claude`, `features/git`, `features/builds`, `features/monitoring`, `features/security`, `features/settings`, `core/network`, `core/storage`, `core/security`.
- Pacotes sugeridos: `signalr_netcore`, `dio`, `flutter_secure_storage`, `mobile_scanner`, `local_auth`, `flutter_webrtc`, `speech_to_text`, `image_picker`, `file_picker`, `receive_sharing_intent`, `webview_flutter`, `fl_chart`, `flutter_markdown`, `re_highlight` (ou equivalente), `firebase_messaging`, `flutter_local_notifications`, `home_widget`, `quick_actions`.
- Suporte a **vários PCs** pareados, com troca rápida.

### 5.2 Telas
1. **Pareamento**: escanear QR ou digitar manualmente.
2. **Dashboard**: online/offline, métricas principais, botões grandes de Desligar / Reiniciar / Suspender / Bloquear, atalhos para as demais áreas, sessões do Claude que precisam de atenção. Ações destrutivas com deslizar para confirmar ou biometria e contagem regressiva com "Cancelar".
3. **Tela remota**: visualização ao vivo, escolha de monitor, alternar visualizar/controlar, touchpad/toque direto, teclado virtual com teclas especiais, indicador de FPS/latência, botão de screenshot.
4. **Agendamentos**: lista, criar/editar (gatilho + ação), ativar/desativar, próximas execuções.
5. **Processos**: lista com busca/ordenação, detalhes, encerrar, janelas abertas.
6. **Apps e URLs**: grade de favoritos com ícones, campo para abrir URL, histórico.
7. **Arquivos**: navegador, breadcrumbs, busca, pré-visualização (imagem, PDF, código), download/upload com progresso, ações de arquivo, compartilhar do celular para o PC.
8. **Claude — sessões**: lista (ativas e recentes) com projeto, status (trabalhando / aguardando você / concluído), custo; botão "Nova sessão".
9. **Claude — chat**: mensagens em streaming, Markdown e blocos de código, cards de ferramentas expansíveis ("Editando src/app.cs", "Executando: dotnet build"), **visualizador de diff**, cards de permissão (Aprovar / Negar / Aprovar sempre), campo de texto com **voz**, **anexo de imagem**, **comandos rápidos**, botão de interromper.
10. **Git**: status, arquivos alterados, diff, histórico, commit/push/pull/branch/descartar.
11. **Builds e testes**: executar, saída em tempo real, histórico, "Pedir para o Claude corrigir".
12. **Preview**: portas em escuta, abrir no WebView, alternar mobile/desktop.
13. **Monitoramento**: gráficos em tempo real e histórico, temperaturas, configuração de alertas.
14. **Segurança**: dispositivos e papéis, auditoria, botão de pânico, biometria/PIN, timeout.
15. **Configurações**: PCs pareados, notificações por tipo, tema, qualidade padrão do espelhamento.

### 5.3 Integrações do sistema
- **Widget na tela inicial** (Android e iOS): status do PC e botões rápidos (bloquear, reiniciar, desligar).
- **Atalhos do app** (pressionar o ícone) e **Siri / Google Assistente** (App Intents no iOS, App Actions/atalhos no Android): "Desligar meu computador", "Reiniciar meu computador", "Bloquear meu computador".
- Receber arquivos do menu "Compartilhar" de outros apps para enviar ao PC.

---

## 6. Entregáveis

1. Código completo do agente e do app, compilando sem erros e sem warnings relevantes.
2. `README.md` com: pré-requisitos; compilar, rodar e publicar o agente; iniciar com o Windows; gerar APK/IPA; configurar Tailscale; configurar hooks e o servidor MCP do Claude Code; configurar Firebase ou ntfy; primeiro pareamento.
3. Testes unitários das partes críticas: pareamento/autenticação, autorização por papel, validação de caminhos de arquivo, parser do stream-json, gerenciador de sessões do Claude, agendador.
4. Documento curto de arquitetura com o contrato do Hub e o modelo de dados.

## 7. Fases de desenvolvimento

Entregue cada fase funcionando antes de seguir para a próxima:

1. **Base**: estrutura, pareamento, autenticação, papéis, auditoria, energia, dashboard e status.
2. **Claude Code**: sessões, streaming, chat, permissões remotas (MCP), hooks.
3. **Notificações push** (FCM/ntfy) e ações nas notificações.
4. **Desenvolvimento**: Git, comandos rápidos, voz, imagens, builds/testes, preview do localhost, custo/uso.
5. **Tela**: screenshot, espelhamento MJPEG, controle remoto; depois WebRTC.
6. **Sistema**: arquivos, processos, apps/URLs, agendamento.
7. **Monitoramento**: histórico, gráficos, alertas, widget, atalhos de voz.
8. **Segurança final e polimento**: botão de pânico, avisos no PC, revisão de segurança, testes, documentação.

## 8. Forma de trabalho
- Antes de começar, apresente a estrutura de pastas, o modelo de dados e o contrato do Hub SignalR para minha aprovação.
- Quando houver alternativas técnicas relevantes, explique brevemente prós e contras e siga com a mais adequada.
- Não invente APIs, pacotes ou flags: verifique a documentação oficial atual.
- Comentários no código e textos da interface em português do Brasil.
