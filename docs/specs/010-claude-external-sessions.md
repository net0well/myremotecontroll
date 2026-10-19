# Spec 010: Sessões do terminal via hooks

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 2 — Claude Code · **ADRs:** 0004, 0013 · **Depende de:** 008, 009

## Problema

Muitas sessões eu começo no terminal do PC. Quero saber pelo celular quando uma delas termina ou precisa de mim,
aprovar permissões dela e, se quiser, assumir a sessão no celular.

## Escopo

### Dentro
- **Instalador de hooks** (opcional, com confirmação na bandeja e no app): mescla em `~/.claude/settings.json`, com
  backup, hooks **HTTP** para `http://127.0.0.1:47801/hooks/claude/<evento>`, com header
  `Authorization: Bearer $MRC_HOOK_TOKEN` e `allowedEnvVars: ["MRC_HOOK_TOKEN"]` (variável de ambiente do usuário
  definida pelo agente), timeout curto (exceto `PermissionRequest`):
  `SessionStart`, `Notification` (matchers `permission_prompt`, `idle_prompt`), `PermissionRequest`, `Stop`,
  `StopFailure`, `SessionEnd`. Desinstalar remove só as entradas do MyRemoteControll.
- Registro de sessões externas (`Origin = External`) com `session_id`, `cwd`, título e estado; eventos de sessões
  gerenciadas são ignorados no receptor (ADR-0004).
- `PermissionRequest` externo: cria `PermissionRequest` (`Source = Hook`), notifica o celular e responde no formato
  `hookSpecificOutput.decision` (`allow` com `updatedInput` / `deny` com `message`) até o timeout do hook; sem resposta,
  devolve sem decisão (o terminal continua pedindo localmente).
- **Assumir sessão:** retoma no modo A com `--resume` (aviso: feche o terminal, senão as mensagens se intercalam) ou
  com `--fork-session` (cópia sem conflito).
- **Sessões recentes do histórico local:** metadados dos arquivos `~/.claude/projects/<projeto>/<session-id>.jsonl`
  (ID, projeto, data, tamanho) + títulos dos hooks; sem depender do conteúdo (formato interno).
- **Remote Control nativo (modo C):** botão "Abrir no Remote Control" que executa `claude remote-control --name "<projeto>"`
  no diretório do projeto e mostra o link/QR.
- Exemplos de configuração em `tools/claude-hooks/`.

### Fora
- Push (spec 011) — esta spec emite os eventos; o envio por push vem na 011.

## Modelo de domínio

`ClaudeSession (Origin = External)`, `PermissionRequest (Source = Hook)`.

## Contrato de API

| Tipo | Nome | Proteção |
|---|---|---|
| HTTP loopback | `POST /hooks/claude/{evento}` (corpo = entrada do hook) | token de hook, só loopback |
| Hub | `ListClaudeSessions(filter)` inclui externas e recentes | `claude.view` |
| Hub | `ResumeClaudeSession(id, { fork })` (assumir) | `claude.chat` |
| Hub | `StartNativeRemoteControl(projectId)` → link/QR | `claude.chat` |
| Hub | `RespondPermission(...)` (mesmo da 009) | `claude.approve` |

## Autorização

Como na 008/009. A instalação dos hooks é feita na bandeja ou por Administrador no app (com confirmação no PC).

## Casos de borda e falhas

- `settings.json` com hooks existentes → mesclar, nunca sobrescrever; backup com data; JSON inválido → não mexer e avisar.
- Agente desligado com hooks instalados → falha não bloqueante e rápida (timeout curto; comportamento padrão dos hooks HTTP).
- Duas sessões no mesmo diretório → diferenciadas pelo `session_id`.
- Token de hook vazado para outro processo do usuário → só permite postar eventos no loopback; rotação pelo menu.
- Hook `PermissionRequest` que expira antes da decisão → o pedido vira `Expired` no app.

## Requisitos não funcionais

- Hooks sem resposta do agente não atrasam o Claude mais que o timeout configurado (padrão 5 s, exceto permissão).
- Receptor de hooks responde em < 50 ms quando não aguarda decisão.

## Integrações

Hooks HTTP do Claude Code; `claude remote-control`.

## Critérios de aceite

- **AC-1:** Dado uma sessão no terminal que termina uma tarefa, então recebo notificação "Claude terminou em <projeto>".
- **AC-2:** Dado um pedido de permissão no terminal, quando aprovo no celular dentro do prazo, então o terminal segue sem intervenção local.
- **AC-3:** Dado o agente parado, quando o Claude dispara um hook, então o Claude não trava e segue o fluxo normal.
- **AC-4:** Dado hooks pré-existentes, quando instalo os do MyRemoteControll, então os antigos são preservados e existe backup.
- **AC-5:** Dado uma sessão gerenciada, quando os hooks globais disparam para ela, então o receptor não cria sessão externa nem decide permissões.
- **AC-6:** Dado uma sessão externa, quando escolho "Assumir (cópia)", então uma sessão gerenciada nova continua a conversa com `--fork-session`.

## Decisões adiadas

- Ler o conteúdo das transcrições para mostrar o histórico completo de sessões externas: fora (formato interno instável).

## Perguntas em aberto

1. Timeout do hook `PermissionRequest`: igual ao da spec 009 (**10 min**) ou menor, para não segurar o terminal?
2. Instalar os hooks automaticamente ao instalar o agente (com consentimento) ou só sob demanda?
