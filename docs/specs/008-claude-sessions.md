# Spec 008: Sessões gerenciadas do Claude Code e chat em streaming

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 2 — Claude Code · **ADRs:** 0004, 0005, 0007, 0021 · **Depende de:** 003, 004, 007

## Problema

Quero conduzir o Claude Code pelo celular como se estivesse no terminal: iniciar ou retomar uma sessão num projeto,
mandar mensagens e ver a resposta chegando em tempo real, sem estar na frente do PC.

## Escopo

### Dentro
- **Whitelist de projetos:** `AllowedRoot` com `Kind = ClaudeProject` e `ClaudeProject` cadastrados na bandeja.
- `ClaudeProcess` (modo A do ADR-0004), com `ArgumentList`, `cwd` = projeto:
  `claude -p --input-format stream-json --output-format stream-json --verbose --include-partial-messages
  --replay-user-messages --session-id <uuid> --model <m> --permission-mode <modo>` (as flags de permissão MCP entram na spec 009).
  Retomada com `--resume <id>`; duplicar com `--resume <id> --fork-session`. Nunca `--bare`.
- Localização do CLI (`IClaudeCli`): PATH e caminhos padrão; verificação de login com `claude auth status`.
- `StreamJsonParser` tolerante: tipos conhecidos (`system/init`, `assistant`, `user`, `stream_event`, `result`,
  `system/api_retry`) mapeados; desconhecidos guardados como JSON cru; linhas inválidas registradas e ignoradas.
- **Estados** da sessão (`Starting`, `Working`, `WaitingInput`, `WaitingPermission`, `Idle`, `Completed`, `Failed`,
  `Terminated`) derivados do stream e do processo.
- **Buffer com `seq`:** mensagens completas em `ClaudeEvent` (últimas 2.000 por sessão); deltas só em memória;
  `GetClaudeSessionEvents(afterSeq)` para reconexão sem perda nem duplicação.
- Várias sessões simultâneas (limite configurável, padrão 3); envio de mensagem durante um turno vai para a fila.
- **Interromper:** mecanismo verificado no início da spec (ADR-0004, pendência 1); fallback: encerrar a árvore de
  processos e marcar a sessão como retomável.
- Encerrar sessão; encerramento do agente fecha as sessões de forma limpa (retomáveis); na inicialização, processos
  `claude` órfãos (PID registrado) são encerrados.
- Escolha de modelo e modo de permissão (`default`, `acceptEdits`, `plan`) na criação.
- **App:** lista de sessões (ativas e recentes) com projeto, estado e última atividade; botão "Nova sessão";
  tela de chat com Markdown, blocos de código com copiar, cards de ferramenta expansíveis ("Editando src/app.cs",
  "Executando: dotnet build"), indicador de "trabalhando", rolagem inteligente, botão Interromper.
- Card "Claude Code" no painel (sessões por estado, última atividade).

### Fora
- Permissões remotas (009), sessões do terminal e histórico (010), push (011), voz/imagens/comandos rápidos (013),
  custo (016).

## Modelo de domínio

`ClaudeProject`, `ClaudeSession` (Id = `session_id` gerado pelo agente), `ClaudeEvent` — ver `data-model.md`.

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `ListClaudeProjects()` | `claude.view` | |
| Hub | `ListClaudeSessions(filter)` | `claude.view` | |
| Hub | `StartClaudeSession(projectId, { model?, permissionMode, title? })` | `claude.chat` | `claude.projectNotAllowed`, `claude.cliNotFound`, `claude.notAuthenticated`, `claude.sessionLimit` |
| Hub | `ResumeClaudeSession(sessionId, { fork, model?, permissionMode? })` | `claude.chat` | `claude.sessionNotFound`, `claude.sessionLimit` |
| Hub | `SendClaudeMessage(sessionId, content[])` → `seq` | `claude.chat` | `claude.sessionNotFound` |
| Hub | `InterruptClaude(sessionId)`, `EndClaudeSession(sessionId)` | `claude.chat` | |
| Hub | `GetClaudeSessionEvents(sessionId, afterSeq, limit)` | `claude.view` | |
| Hub | `OpenClaudeSession(id)` / `CloseClaudeSession(id)` | `claude.view` | |
| Evento | `ClaudeEvent(sessionId, seq, evt)`, `ClaudeSessionStateChanged` | conforme grupo | |

Nesta spec, `content[]` aceita só blocos de texto (imagens na 013).

## Autorização

`claude.view` vê sessões e eventos; `claude.chat` inicia, envia, interrompe e encerra.

## Casos de borda e falhas

- CLI ausente → `claude.cliNotFound` com orientação; não autenticado → `claude.notAuthenticated` ("rode `claude` no PC e faça login").
- Processo morre → `Failed` + últimas linhas do stderr no app.
- Linha JSON inválida → registrada e ignorada, sem derrubar a sessão.
- Mensagem enviada enquanto o Claude trabalha → fila (o CLI processa em sequência).
- App desconectado durante streaming → reenvio a partir do `seq`; deltas perdidos são substituídos pela mensagem completa.
- Projeto removido da whitelist com sessão ativa → a sessão continua até terminar; novas sessões recusadas.
- Agente encerrado → sessões `Terminated`, retomáveis com `--resume`.
- Retomar uma sessão que está aberta em outro terminal → aviso de intercalação (ADR-0004).

## Requisitos não funcionais

- Latência evento → celular < 300 ms na LAN.
- Leitura de stdout em tarefa dedicada; nada bloqueia a thread do Hub.
- Nenhum processo `claude` órfão após encerrar o agente.
- Conteúdo de mensagens nunca vai para o log.

## Integrações

Claude Code CLI (documentação oficial obrigatória antes de implementar; testes com gravações reais de stream-json).

## Critérios de aceite

- **AC-1:** Dado um projeto permitido, quando inicio uma sessão e envio "liste os arquivos", então vejo a resposta chegando em streaming.
- **AC-2:** Dado o app desconectado no meio de uma resposta, quando reconecta, então recebe os eventos perdidos na ordem, sem duplicar.
- **AC-3:** Dado um diretório fora da whitelist, quando tento iniciar sessão nele, então recebo `claude.projectNotAllowed`.
- **AC-4:** Dado uma sessão trabalhando, quando toco em Interromper, então a resposta para e a sessão volta a aceitar mensagens.
- **AC-5:** Dado o CLI ausente, quando inicio sessão, então recebo `claude.cliNotFound` com orientação.
- **AC-6:** Dado 3 sessões ativas (limite padrão), quando inicio a 4ª, então recebo `claude.sessionLimit`.
- **AC-7:** Dado o agente reiniciado, então não há processos `claude` órfãos e as sessões anteriores podem ser retomadas.

## Decisões adiadas

- Envio de comandos `/skill` e `/model` dentro do chat: o CLI expande `/skill` no prompt; tratar como texto comum.

## Perguntas em aberto

1. Limite padrão de **3 sessões simultâneas** está ok?
2. Modelos oferecidos no app: aliases (`sonnet`, `opus`, `haiku`, `fable`) ou lista configurável na bandeja?
3. Mostrar o **raciocínio (thinking)** do Claude no chat, recolhido por padrão?
