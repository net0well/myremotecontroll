# Spec 009: Aprovar ou negar ações do Claude pelo celular

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 2 — Claude Code · **ADRs:** 0004, 0009, 0013 · **Depende de:** 008

## Problema

Quando o Claude pede permissão para editar um arquivo ou rodar um comando, a sessão fica parada até alguém responder
no PC. Quero aprovar ou negar pelo celular, vendo exatamente o que será feito.

## Escopo

### Dentro
- **Servidor MCP local** (ModelContextProtocol.AspNetCore, Streamable HTTP) no loopback `127.0.0.1:47801/mcp`,
  `AllowedHosts` restrito, token por sessão no header, com a ferramenta `approve`.
- Cada sessão gerenciada recebe um `--mcp-config` temporário (removido ao encerrar) e
  `--permission-prompt-tool mcp__myremotecontroll__approve`.
- A chamada cria um `PermissionRequest` (`Source = McpTool`), muda a sessão para `WaitingPermission`, emite
  `ClaudePermissionRequested` (e push na spec 011) e aguarda a decisão.
- Respostas: `allow` (com `updatedInput` igual ao original), `deny` (com mensagem), ou **"Sempre nesta sessão"**
  (`SessionPermissionRule` por ferramenta + padrão; aprovações automáticas auditadas).
- Timeout configurável (padrão 10 min) → `deny` com "Tempo esgotado".
- Receptor de hooks ignora `PermissionRequest` de sessões gerenciadas (resposta sem decisão), para não disputar com a
  ferramenta MCP (ADR-0004).
- **App:** card de permissão no chat e no topo do painel com ferramenta, comando/arquivo e diff quando for edição;
  botões Aprovar / Negar / Sempre; exige `claude.approve` (+ biometria se configurado); mostra "Aprovado por <aparelho>".
- Aviso no app de que regras estáticas do `settings.json` podem aprovar/negar sem passar pelo celular (R2).

### Fora
- Permissões de sessões do terminal (spec 010).
- Ações direto na notificação push (spec 011).

## Modelo de domínio

`PermissionRequest`, `SessionPermissionRule` (ver `data-model.md`).

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| MCP (loopback) | ferramenta `approve(tool_name, input, ...)` → resposta no formato documentado | token da sessão | |
| Hub | `ListPendingPermissions()` | `claude.view` | |
| Hub | `RespondPermission(requestId, decision, scope, message?, localConfirmation)` | `claude.approve` | `claude.permissionResolved` |
| Evento | `ClaudePermissionRequested`, `ClaudePermissionResolved` | `perm:claude.view` | |

O formato exato da resposta da ferramenta é confirmado na documentação antes da implementação (ADR-0004, pendência 2).

## Autorização

Ver e receber o card: `claude.view`. Responder: `claude.approve` (só Administrador por padrão).

## Casos de borda e falhas

- Dois aparelhos respondem ao mesmo tempo → a primeira decisão vence; o segundo recebe `claude.permissionResolved`.
- Sessão encerrada com pedido pendente → pedido `Cancelled`.
- Agente reinicia com pedido pendente → pedido `Expired`; o Claude recebe negação (ou erro de conexão do MCP, tratado como negação).
- Regras estáticas resolvem o pedido → o MCP nem é chamado (documentado no app).
- Ferramenta MCP marcada como "requer interação do usuário" → o CLI converte `allow` em negação (limitação documentada).
- Entrada enorme (diff grande) → truncada no card, com "ver completo" via Git (spec 012).

## Requisitos não funcionais

- Pedido → card no celular < 500 ms na LAN.
- Endpoint MCP inacessível pela rede (só loopback).

## Integrações

Claude Code `--permission-prompt-tool`, `--mcp-config`; push (spec 011).

## Critérios de aceite

- **AC-1:** Dado um pedido para executar `dotnet test`, quando aprovo no celular, então o Claude executa e o card mostra "Aprovado por <aparelho>".
- **AC-2:** Dado um pedido sem resposta por 10 min, então o Claude recebe negação com a mensagem "Tempo esgotado".
- **AC-3:** Dado "Sempre nesta sessão" para `Edit`, quando o Claude pede outra edição, então ela é aprovada automaticamente e auditada.
- **AC-4:** Dado o endpoint MCP, quando acessado de outra máquina da rede, então a conexão é recusada.
- **AC-5:** Dado dois aparelhos com o card aberto, quando um aprova, então o outro vê o card resolvido.
- **AC-6:** Dado um aparelho sem `claude.approve`, então o card aparece sem os botões de decisão.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Responder **`AskUserQuestion`** (perguntas de múltipla escolha do Claude) pelo celular nesta spec?
2. Timeout padrão de **10 min** está ok?
3. **Biometria** obrigatória para aprovar, ou configurável (padrão desligada)?
4. "Sempre nesta sessão" por **ferramenta inteira** (ex.: todo `Edit`) ou por **ferramenta + padrão** (ex.: `Bash(dotnet test *)`)? Recomendado: ferramenta + padrão sugerido pelo próprio pedido.
