# ADR-0004: Integração com o Claude Code

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente precisa conduzir o Claude Code CLI pelo celular: iniciar e retomar sessões, transmitir a resposta em tempo
real, encaminhar pedidos de permissão, acompanhar sessões abertas no terminal e medir custo. O Claude Code já tem o
recurso nativo **Remote Control**, mas ele não integra Git, builds, arquivos, tela e energia, e depende do app Claude.

### Fatos confirmados na documentação oficial (code.claude.com/docs, consultada em 2026-10-18; CLI instalado: 2.1.302)

| Item | Confirmado |
|---|---|
| Modo headless | `-p`/`--print`; `--input-format text\|stream-json`; `--output-format text\|json\|stream-json` |
| Streaming | `--verbose` e `--include-partial-messages` (eventos `stream_event` com `text_delta`); `--replay-user-messages` reemite no stdout as mensagens recebidas pelo stdin |
| ID da sessão | `--session-id <uuid>` define o ID na criação; `--resume <id\|nome\|caminho .jsonl>` mantém o mesmo ID; `--fork-session` cria um ID novo |
| Modos de permissão | `default` (alias `manual`), `acceptEdits`, `plan`, `auto`, `dontAsk`, `bypassPermissions` |
| Permissões no `-p` | `--permission-prompt-tool <ferramenta MCP>`; `--permission-prompts host\|none` (v2.1.259+) |
| MCP | `--mcp-config <arquivo\|json>`, `--strict-mcp-config`; com `-p`, o CLI espera o servidor conectar (até `MCP_TIMEOUT`, padrão 30 s) |
| Mensagem de entrada | `{"type":"user","message":{"role":"user","content":[...]},"parent_tool_use_id":null}`; aceita blocos `image` com `source` base64 + `media_type` |
| Primeira mensagem | `system/init` com modelo, ferramentas, `mcp_servers`, `capabilities` (ex.: `interrupt_receipt_v1`) |
| Última mensagem | `result` com texto final, `session_id`, uso e `total_cost_usd` (estimativa do cliente; em `--resume` reporta o total acumulado da conversa) |
| Outros eventos | `system/api_retry`, `permission_denied` (com `--permission-prompts none`) |
| Encerramento | SIGTERM encerra sem registrar resultado; SIGINT ou o `interrupt()` do SDK encerram o turno |
| `--bare` | Pula hooks, skills, plugins, MCP e CLAUDE.md **e não usa o login da assinatura** (exige `ANTHROPIC_API_KEY`) |
| Hooks | Tipos `command`, `http`, `mcp_tool`, `prompt`, `agent`; eventos incluem `SessionStart`, `Notification`, `PermissionRequest`, `Stop`, `StopFailure`, `SessionEnd` |
| Hook HTTP | `{"type":"http","url":...,"timeout":...,"headers":{"Authorization":"Bearer $VAR"},"allowedEnvVars":["VAR"]}`; falha (sem conexão, timeout, não-2xx) é **não bloqueante** por padrão |
| `PermissionRequest` | Entrada com `tool_name`, `tool_input`, `permission_suggestions`; saída `hookSpecificOutput.decision.behavior` = `allow` (com `updatedInput`/`updatedPermissions`) ou `deny` (com `message`, `interrupt`); timeout padrão 600 s |
| Hooks no `-p` | Rodam também em sessões `-p`; **quando há `--permission-prompt-tool`, o hook roda em paralelo e vale a primeira decisão** |
| Transcrições | `~/.claude/projects/<cwd com não-alfanuméricos trocados por ->/<session-id>.jsonl`; formato interno, muda entre versões; retenção padrão 30 dias |
| Sessões `-p` | Não aparecem no seletor nem no `--continue` interativo; retomáveis por ID |
| Remote Control | `claude remote-control --name "<nome>"` (modo servidor) e `claude --remote-control` |

## Decisão

Wrapper próprio em `MyRemoteControll.Claude`, em três modos:

**A. Sessões gerenciadas.** O agente gera o UUID e inicia, no diretório do projeto (whitelist), com `ArgumentList`:

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --replay-user-messages
       --session-id <uuid> --model <modelo> --permission-mode <modo>
       --mcp-config <arquivo temporário> --permission-prompt-tool mcp__myremotecontroll__approve
```

- Retomada: `--resume <id>` no lugar de `--session-id` (mesmo ID); "duplicar sessão" usa `--fork-session`.
- **Não usar `--bare`**: perderia o login da assinatura, o `CLAUDE.md` e as skills do projeto.
- `StreamJsonParser` tolerante: tipos desconhecidos são guardados como JSON cru em `ClaudeEvent`.
- Custo e uso vêm da mensagem `result`; como `--resume` reporta o total acumulado, a spec 016 calcula deltas.
- Uma mensagem por vez no stdin; mensagens enviadas durante um turno ficam na fila do próprio CLI.

**Permissões (modo A).** Servidor MCP local (ModelContextProtocol.AspNetCore, Streamable HTTP) no listener loopback
`127.0.0.1:47801/mcp`, com token por sessão no header, expondo a ferramenta `approve`. A chamada cria um
`PermissionRequest`, avisa o app (Hub + push) e bloqueia até a decisão ou o timeout (padrão 10 min → negar).

**B. Sessões externas (terminal).** Hooks HTTP instalados (com consentimento, mesclando e com backup) em
`~/.claude/settings.json`, apontando para `http://127.0.0.1:47801/hooks/claude/<evento>` com token via
`allowedEnvVars`. `PermissionRequest` aguarda a decisão do celular até o timeout do hook.
"Assumir sessão" retoma no modo A com `--resume` (avisando que o terminal precisa ser fechado, porque duas instâncias
na mesma sessão intercalam mensagens) ou com `--fork-session` (cópia, sem conflito).

**Conflito entre A e B.** Como os hooks globais também disparam nas sessões gerenciadas e "vale a primeira decisão",
o receptor de hooks reconhece os IDs das sessões gerenciadas (o agente os gerou com `--session-id`) e responde
**sem decisão** (2xx com corpo vazio) para elas, deixando a ferramenta MCP decidir. Eventos de ciclo de vida dessas
sessões também são ignorados no receptor de hooks, para não duplicar notificações.

**C. Remote Control nativo.** Botão que executa `claude remote-control --name "<projeto>"` no diretório do projeto e
mostra o link/QR para abrir no app Claude.

**Listagem de sessões recentes.** Ler apenas metadados dos arquivos `.jsonl` (nome = ID, pasta = projeto, data de
modificação, tamanho) e os títulos registrados pelos hooks. Não depender do conteúdo, cujo formato é interno.

## Alternativas consideradas

- **Hook `PermissionRequest` também no modo A** (passado por `--settings` em cada sessão, sem MCP): um único
  mecanismo para A e B, formato de resposta bem documentado e sem o SDK de MCP. Contra: a ferramenta de permissão é o
  "host" oficial do `-p` (necessário, por exemplo, para retomar em modo `plan` com `-p` e para responder
  `AskUserQuestion`). Mantida como plano B caso o MCP dê problemas.
- **Agent SDK oficial (TypeScript/Python) como sidecar:** exigiria Node ou Python na máquina; o protocolo por baixo é o
  mesmo stream-json.
- **SDKs .NET comunitários:** não oficiais, atrasam em relação ao CLI.
- **Só o Remote Control nativo:** depende do app Claude e de plano Pro/Max; sem Git, arquivos, tela ou energia.
- **API direta da Anthropic:** cobra por token e não usa a sessão nem as ferramentas do Claude Code.

## Consequências (positivas / negativas / riscos)

- (+) Controle total das sessões, com o ID conhecido desde o início (`--session-id`), o que simplifica o modelo de dados.
- (+) Convive com o Remote Control nativo e com o uso normal no terminal.
- (−) Acoplamento a um CLI que muda com frequência (R1): isolado em `MyRemoteControll.Claude`, com testes sobre
  gravações reais de stream-json e verificação das `capabilities` do `system/init`.
- (risco) A ferramenta de permissão só é chamada quando nenhuma regra estática decide (R2): o app mostra isso.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **Permissões no modo A: servidor MCP** (`--permission-prompt-tool`). O hook `PermissionRequest` via `--settings`
  fica como plano B.
- **Pendências técnicas, que não mudam a decisão e são verificadas na spec indicada:**
  1. **Interrupção (spec 008):** o SDK interrompe por mensagens de controle no stdin; o formato bruto não está na
     documentação consultada. No Windows, enviar SIGINT a um processo filho não é trivial. Fallback: encerrar a árvore
     de processos e retomar com `--resume`.
  2. **Formato da resposta da ferramenta de permissão (spec 009):** confirmar
     `{"behavior":"allow","updatedInput":{...}}` / `{"behavior":"deny","message":"..."}` na referência do Agent SDK.
  3. **Responder `AskUserQuestion` pelo celular:** decidir no `/spec` da spec 009.

## Specs afetadas

008, 009, 010, 011, 012 (mensagem de commit gerada), 013, 014, 016, 023, 026.
