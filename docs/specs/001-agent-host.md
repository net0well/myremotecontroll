# Spec 001: Host do agente, bandeja, configuração, logs e banco

**Status:** Approved (2026-10-18)
**Data:** 2026-10-18
**Fase:** 0 — Fundação · **ADRs:** 0002, 0003, 0005, 0007, 0020 · **Depende de:** —

## Problema

Precisa existir um processo confiável no PC, iniciado com o Windows na sessão do usuário, que hospede a API e mostre
seu estado. Sem ele, nenhuma outra funcionalidade existe. Hoje não há nada: qualquer controle remoto exige estar na
frente do PC.

## Escopo

### Dentro
- Solução `agent/MyRemoteControll.slnx` gerada com `/dotnet-init` (Vertical Slice): projetos `Agent`
  (`net10.0-windows`), `Windows`, `Claude`, `Contracts`, `Agent.Tests` e `Agent.IntegrationTests`;
  `Directory.Build.props` e `Directory.Packages.props` (CPM). O projeto `Sensors` só nasce na spec 024.
- Generic Host com Kestrel em **dois listeners**: público `0.0.0.0:47800` (HTTPS) e loopback `127.0.0.1:47801` (HTTP).
- **Certificado autoassinado** (ADR-0006): ECDSA P-256, validade 5 anos, gerado na primeira execução em
  `CurrentUser\My` com chave não exportável e usado pelo listener público; cálculo do pin SHA-256 da SPKI
  (consumido pela spec 002 no QR).
- Ícone de bandeja (WinForms `NotifyIcon`) com menu: status (online / pausado / erro), **Pausar acesso remoto** /
  **Retomar**, **Iniciar com o Windows** (liga/desliga a tarefa agendada), **Abrir logs**, **Sair**.
- **Instância única** por usuário (mutex nomeado); a segunda instância sinaliza a primeira (abre o menu) e sai.
- **Autostart** por tarefa agendada "ao fazer logon" do usuário atual, sem elevação, criada/removida pelo item do menu
  (o instalador da spec 027 reutiliza a mesma rotina).
- `appsettings.json` + Options pattern com validação na inicialização (`ValidateOnStart`): portas, diretório de dados,
  nível de log.
- Serilog com bootstrap em dois estágios e arquivo rotativo em `%LOCALAPPDATA%\MyRemoteControll\logs` (ADR-0020),
  correlation ID por requisição.
- SQLite + EF Core 10 em `%LOCALAPPDATA%\MyRemoteControll\data.db` (WAL), migrations aplicadas na inicialização com
  backup antes de migrar; tabela `AgentSettings`.
- `GET /health` só no loopback; `GET /api/v1/info` no listener público.
- Hub `/hubs/remote` mapeado (sem métodos de negócio) com o evento `AgentShuttingDown` no encerramento gracioso.
- Middleware de **pausa**: com o acesso remoto pausado, o listener público responde `503` com `agent.paused`.
  O loopback (hooks e MCP do Claude) continua funcionando.

### Fora
- Autenticação, pareamento (QR, pin no app, tokens) — spec 002.
- Qualquer feature de negócio.
- Instalador (spec 027).

## Modelo de domínio

- `AgentSetting { Key (PK), ValueJson, UpdatedAt }` — sobreposição persistida ao `appsettings.json`, lida por um
  provedor de configuração próprio (`IOptionsMonitor`).
- Estado em memória: `AgentRuntimeState { Status (Starting, Running, Paused, Faulted), PausedAt?, PauseReason? }`.

## Contrato de API

| Verbo | Rota | Listener | Resposta | Erros |
|---|---|---|---|---|
| GET | `/api/v1/info` | público | `{ apiVersion: "1", requiresPairing: true }` (sem dados sensíveis antes da autenticação) | `503 agent.paused` |
| GET | `/health` | loopback | `200 { status: "Healthy" }` | pela rede: `404` |
| WS | `/hubs/remote` | público | `401` até a spec 002 | `503 agent.paused` |

Qualquer outra rota no listener público: `401` (sem autenticação configurada ainda) ou `404`.

## Autorização

Nenhuma ainda. Tudo além de `/api/v1/info` retorna `401`. O loopback não é acessível pela rede.

## Casos de borda e falhas

- **Porta ocupada:** sem porta alternativa (os aparelhos pareados guardam a porta). O agente entra em estado `Faulted`,
  avisa na bandeja qual porta está ocupada (e por qual processo, quando possível) e tenta de novo a cada 30 s.
- **Certificado ausente ou com chave inacessível:** gera um novo e avisa na bandeja que os aparelhos precisam reparear.
- **Banco corrompido:** renomear para `data.corrupt-<aaaammdd-hhmmss>.db`, recriar, registrar no log e avisar na bandeja.
- **Migration falha:** restaurar o backup, entrar em estado `Faulted`, avisar na bandeja com "Abrir logs".
- **Segunda instância:** sinaliza a primeira e sai com código 0.
- **Encerramento** (Sair, logoff, desligamento do Windows): `AgentShuttingDown` para os clientes, flush do log, fecha o banco.
- **Configuração inválida:** falha na inicialização com mensagem clara no log e na bandeja.
- **Diretório de dados sem permissão de escrita:** estado `Faulted` com aviso.

## Requisitos não funcionais

- Inicialização < 3 s até o ícone aparecer e os listeners responderem.
- Ocioso: < 80 MB de RAM e < 1% de CPU (medir; usar Workstation GC se o Server GC estourar a meta).
- Nenhuma escrita síncrona de log ou banco na thread da UI da bandeja.

## Integrações

- Agendador de Tarefas do Windows (tarefa "ao fazer logon", `RunLevel` limitado, reinício em caso de falha).

## Critérios de aceite

- **AC-1:** Dado o agente com "Iniciar com o Windows" ativo, quando faço logon no Windows, então o ícone aparece na bandeja em até 10 s.
- **AC-2:** Dado o agente rodando, quando executo outra instância, então ela encerra e a existente continua.
- **AC-3:** Dado o banco inexistente, quando o agente inicia, então as migrations criam o banco sem intervenção.
- **AC-4:** Dado o agente rodando, quando chamo `/health` pelo loopback, então recebo 200; pela rede, recebo 404.
- **AC-5:** Dado "Pausar acesso remoto" na bandeja, quando um cliente tenta conectar, então recebe 503 `agent.paused` até eu retomar.
- **AC-6:** Dado um cliente sem autenticação, quando chama `GET /api/v1/info`, então recebe apenas `apiVersion` e `requiresPairing`.
- **AC-7:** Dado um arquivo de banco corrompido, quando o agente inicia, então o arquivo é preservado com outro nome, um banco novo é criado e a bandeja avisa.
- **AC-8:** Dado o agente rodando, quando escolho "Sair", então os clientes conectados recebem `AgentShuttingDown` antes do encerramento.
- **AC-9:** Dado o item "Iniciar com o Windows" desligado, então não existe tarefa agendada do MyRemoteControll.
- **AC-10:** Dado a primeira execução, então existe um certificado ECDSA P-256 no repositório do usuário e o listener público responde só em HTTPS com ele.
- **AC-11:** Dado a porta 47800 ocupada, quando o agente inicia, então a bandeja mostra o erro com a porta e o agente não escuta em outra porta.
- **AC-12:** Dado o acesso remoto pausado, quando um hook do Claude chama o loopback, então a chamada é atendida normalmente.

## Decisões adiadas

- Biblioteca para a tarefa agendada (`schtasks` com XML via `ArgumentList` ou biblioteca COM): decidida no `/plan`.

## Respostas do /spec (2026-10-18)

1. Portas **47800** (público) e **47801** (loopback).
2. Porta ocupada → **falha com aviso na bandeja**, sem porta alternativa.
3. Certificado autoassinado gerado **nesta spec**.
4. A pausa bloqueia **só o listener público**.

## Perguntas em aberto

Nenhuma.
